using CsCheck;
using Rtok.Hook.Contracts;

namespace Rtok.Hooks.Tests;

public sealed class HookWireTests
{
    private static readonly HookRequest Sample = new(
        "0.1.0+abc",
        0x0123_4567_89ab_cdef,
        "PreToolUse",
        "claude",
        "/work/проект",
        [new("DEVIN_PROJECT_DIR", "/p")],
        4242,
        """{"tool_name":"Bash"}"""u8.ToArray()
    );

    [Fact]
    public void A_request_round_trips()
    {
        var back = HookWire.DecodeRequest(HookWire.EncodeRequest(Sample));

        Assert.NotNull(back);
        Assert.Equal(
            Sample with
            {
                Stdin = default,
                HostEnv = [],
            },
            back with
            {
                Stdin = default,
                HostEnv = [],
            }
        );
        Assert.Equal(Sample.HostEnv, back.HostEnv);
        Assert.Equal(Sample.Stdin.ToArray(), back.Stdin.ToArray());
    }

    [Fact]
    public void Any_request_round_trips() =>
        Gen.Select(
                Gen.String,
                Gen.ULong,
                Gen.String,
                Gen.Byte.Array[0, 512],
                Gen.Int[0, int.MaxValue]
            )
            .Sample(t =>
            {
                var (s, fp, cwd, stdin, pid) = t;
                var request = new HookRequest(s, fp, s, s, cwd, [new(s, cwd)], pid, stdin);
                var back = HookWire.DecodeRequest(HookWire.EncodeRequest(request));
                return back is not null
                    && string.Equals(back.Version, s, StringComparison.Ordinal)
                    && back.Fingerprint == fp
                    && string.Equals(back.Cwd, cwd, StringComparison.Ordinal)
                    && back.Pid == pid
                    && string.Equals(back.HostEnv.Single().Value, cwd, StringComparison.Ordinal)
                    && back.Stdin.Span.SequenceEqual(stdin);
            });

    // Every cut of a valid body, and one byte too many, is malformed rather than misread.
    [Fact]
    public void A_truncated_or_padded_request_is_malformed()
    {
        var body = HookWire.EncodeRequest(Sample);
        for (var n = 0; n < body.Length; n++)
        {
            Assert.Null(HookWire.DecodeRequest(body.AsMemory(0, n)));
        }
        Assert.Null(HookWire.DecodeRequest(body.Append((byte)0).ToArray()));
    }

    [Fact]
    public void A_huge_host_env_count_is_malformed_without_allocating_it()
    {
        var body = HookWire.EncodeRequest(Sample with { HostEnv = [] });
        // The count sits after version, fingerprint, event, host and cwd.
        var at = 4 + 9 + 8 + 4 + 10 + 4 + 6 + 4 + "/work/проект"u8.Length;
        body[at] = 0xff;
        body[at + 1] = 0xff;
        body[at + 2] = 0xff;
        body[at + 3] = 0x7f;

        Assert.Null(HookWire.DecodeRequest(body));
    }

    [Theory]
    [InlineData(HookReplyStatus.Served, "{\"a\":1}")]
    [InlineData(HookReplyStatus.RefusedVersion, "")]
    [InlineData(HookReplyStatus.RefusedFingerprint, "")]
    public void A_reply_round_trips(HookReplyStatus status, string stdout)
    {
        var body = HookWire.EncodeReply(status, System.Text.Encoding.UTF8.GetBytes(stdout));

        Assert.True(HookWire.TryDecodeReply(body, out var back, out var output));
        Assert.Equal(status, back);
        Assert.Equal(stdout, System.Text.Encoding.UTF8.GetString(output.Span));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 3 })]
    [InlineData(new byte[] { 1, (byte)'x' })]
    public void A_bad_reply_is_malformed(byte[] body) =>
        Assert.False(HookWire.TryDecodeReply(body, out _, out _));

    [Fact]
    public void A_frame_is_the_little_endian_length_then_the_body() =>
        Assert.Equal(new byte[] { 2, 0, 0, 0, 7, 8 }, HookWire.Frame([7, 8]));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(5, 4)]
    [InlineData(200_000, 200_000)]
    [InlineData(200_001, 200_000)]
    public void Stdin_reads_up_to_the_cap(int size, int cap)
    {
        var data = new byte[size];
        Random.Shared.NextBytes(data);

        var read = HookWire.ReadCapped(new MemoryStream(data), cap);

        if (size > cap)
        {
            Assert.Null(read);
        }
        else
        {
            Assert.Equal(data, read);
        }
    }
}
