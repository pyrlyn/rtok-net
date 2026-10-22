using Microsoft.AspNetCore.Http;
using Rtok.Config;
using Rtok.Store;
using Rtok.Testing;

namespace Rtok.Proxy.Tests;

public sealed class CompressorTests
{
    [Fact]
    public void Diffs_and_source_are_code_logs_are_not()
    {
        Assert.True(Compressor.IsCode("diff --git a/x b/x\n--- a/x\n+++ b/x\n@@ -1 +1 @@\n-a\n+b"));
        Assert.True(Compressor.IsCode("fn a() {\n    let x = 1;\n    x;\n}\n"));
        Assert.False(Compressor.IsCode("running 3 tests\ntest a ... ok\nerror: boom\n"));
    }

    [Fact]
    public void The_prompt_carries_both_placeholders_once_and_versions_by_model()
    {
        Assert.Single(CompressPrompt.User.Split("{{must_keep}}")[1..]);
        Assert.Single(CompressPrompt.User.Split("{{text}}")[1..]);
        Assert.NotEmpty(CompressPrompt.System);
        Assert.Matches("^[0-9a-f]{12}\\+m-1$", CompressPrompt.Version("m-1"));
        Assert.NotEqual(
            CompressPrompt.Version("m-1"),
            CompressPrompt.Version("m-2"),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void A_result_that_names_a_placeholder_stays_text()
    {
        var user = CompressPrompt.Fill("see {{must_keep}}", [new("id", "abc")]);

        Assert.Contains("- abc\n", user, StringComparison.Ordinal);
        Assert.Contains(
            "<tool_result>\nsee {{must_keep}}\n</tool_result>",
            user,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Only_anthropic_credentials_become_compressor_auth()
    {
        var headers = new HeaderDictionary
        {
            ["authorization"] = "Bearer sk-test",
            ["x-session"] = "s1",
            [Lanes.Header] = "bulk",
        };

        var auth = CompressAuth.FromHeaders(headers, Wire.AnthropicProvider);

        Assert.NotNull(auth);
        Assert.Equal("authorization", Assert.Single(auth.Headers).Key);
        foreach (var provider in new[] { Wire.OpenAiProvider, Wire.GeminiProvider, null })
        {
            Assert.Null(CompressAuth.FromHeaders(headers, provider));
        }
    }

    [Fact]
    public void Auth_needs_a_credential()
    {
        Assert.Null(CompressAuth.Of([new("anthropic-version", "x")]));
        Assert.NotNull(CompressAuth.Of([new("X-Api-Key", "k")]));
    }

    [Fact]
    public void The_flag_off_builds_no_compressor()
    {
        using var f = new QueueFixture(enabled: false);

        Assert.Null(f.Compressor);
    }

    [Fact]
    public void Concurrent_distinct_ids_never_exceed_the_cap()
    {
        using var f = new QueueFixture();
        const int threads = 32;
        for (var round = 0; round < 20; round++)
        {
            var q = new SummaryQueue(f.Compressor!, 1);
            using var start = new Barrier(threads);
            var admitted = new SummaryAdmit[threads];
            // Own threads, not the pool: every one blocks on the barrier at once.
            var workers = Enumerable
                .Range(0, threads)
                .Select(i => new Thread(() =>
                {
                    start.SignalAndWait();
                    admitted[i] = q.Admit($"id-{i}");
                }))
                .ToList();
            workers.ForEach(t => t.Start());
            workers.ForEach(t => t.Join());
            Assert.Single(admitted, a => a is SummaryAdmit.Run);
            Assert.Equal(1, q.InFlight);
            q.Release();
            Assert.Equal(0, q.InFlight);
        }
    }

    [Fact]
    public void An_id_refused_at_the_cap_is_retried_once_a_slot_frees()
    {
        using var f = new QueueFixture();
        var q = new SummaryQueue(f.Compressor!, 1);

        Assert.Equal(SummaryAdmit.Run, q.Admit("a"));
        Assert.Equal(SummaryAdmit.Full, q.Admit("b"));
        Assert.Equal(1, q.InFlight);
        q.Release();

        Assert.Equal(SummaryAdmit.Run, q.Admit("b"));
    }

    [Fact]
    public void A_duplicate_hands_its_reservation_back()
    {
        using var f = new QueueFixture();
        var q = new SummaryQueue(f.Compressor!, 2);

        Assert.Equal(SummaryAdmit.Run, q.Admit("a"));
        Assert.Equal(SummaryAdmit.Seen, q.Admit("a"));

        Assert.Equal(1, q.InFlight);
    }

    [Fact]
    public void Without_a_credential_nothing_starts()
    {
        using var f = new QueueFixture();
        var q = new SummaryQueue(f.Compressor!, 2);

        q.Submit(null, ["a", "b"]);

        Assert.Equal(0, q.InFlight);
        Assert.Equal(SummaryAdmit.Run, q.Admit("a"));
    }

    [Fact]
    public void A_stored_summary_replaces_the_pointer_and_a_missing_one_is_offered()
    {
        using var f = new QueueFixture();
        var id = f.Store.PutArchive("s1", [.. "x\n"u8]);
        var pointer = new string('p', 400);
        List<string> offered = [];

        Assert.Null(SummaryView.Stored(f.Store, "v1", id, pointer.Length, offered));
        Assert.Equal([id], offered);

        f.Store.PutArchiveSummary(id, "v1", "ran 400 lines of shell output");
        offered.Clear();
        var view = SummaryView.Stored(f.Store, "v1", id, pointer.Length, offered);

        Assert.Equal(
            $"[compress {id[..12]}]\ntype: tool_output\nsummary: ran 400 lines of shell output\nexpand({id})",
            view
        );
        Assert.Empty(offered);
        Assert.Null(SummaryView.Stored(f.Store, "v1", id, view!.Length, offered));
    }

    private sealed class QueueFixture : IDisposable
    {
        private readonly TempDir _dir = new("rtok-compress-");

        public QueueFixture(bool enabled = true)
        {
            Store = RtokStore.Open(
                new StoreOptions(_dir.Combine("rtok.db")) { ArchiveDir = _dir.Combine("archive") }
            );
            var config = new RtokConfig();
            config.Plugins.Compress.Llm.Enabled = enabled;
            Compressor = Compressor.Create(config, Store, Serilog.Core.Logger.None);
        }

        public RtokStore Store { get; }

        public Compressor? Compressor { get; }

        public void Dispose()
        {
            Compressor?.Dispose();
            Store.Dispose();
            _dir.Dispose();
        }
    }
}
