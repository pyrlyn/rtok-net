using System.Net;
using System.Text;
using System.Text.Json;

namespace Rtok.Web.Tests;

public sealed class SocketTests
{
    [Fact]
    public async Task Without_the_token_or_with_another_runs_token_the_socket_is_refused()
    {
        await using var web = await WebFixture.StartAsync();
        await using var other = await WebFixture.StartAsync();
        var bare = new UriBuilder(web.Socket()) { Query = "" }.Uri;

        Assert.Equal(HttpStatusCode.Unauthorized, await GuardTests.RefusedAsync(bare));
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await GuardTests.RefusedAsync(web.Socket(other.Server.Token))
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await GuardTests.RefusedAsync(web.Socket(web.Server.Token + "x"))
        );
        Assert.Equal(401, await web.RawStatusAsync("/ws"));
    }

    [Fact]
    public async Task A_plain_request_to_the_socket_path_gets_400()
    {
        await using var web = await WebFixture.StartAsync();

        Assert.Equal(400, await web.RawStatusAsync("/ws?token=" + web.Server.Token));
    }

    [Fact]
    public async Task The_first_frame_is_a_snapshot()
    {
        await using var web = await WebFixture.StartAsync();
        using var socket = await web.ConnectAsync();

        var frame = Assert.IsType<SnapshotFrame>(await WebFixture.ReceiveAsync(socket));

        Assert.Null(frame.Req);
        Assert.Equal(100, frame.Snapshot.Overview.Input);
        Assert.Contains(frame.Snapshot.Plugins, p => p is { Id: "docs", Enabled: true });
    }

    [Fact]
    public async Task Every_request_id_is_echoed_in_its_reply()
    {
        await using var web = await WebFixture.StartAsync();
        using var socket = await web.ConnectAsync();
        _ = await WebFixture.ReceiveAsync(socket);

        string[] messages =
        [
            """{"req":1,"expand":"abc"}""",
            """{"req":2,"toggle":{"plugin":"nope","enabled":true}}""",
            """{"req":3,"doctor":{"action":"plan"}}""",
            """{"req":4,"graph":{"project":"p"}}""",
            """{"req":5,"project":{"action":"link","from":"a","to":"b"}}""",
            """{"req":6,"calls":{"subscribe":true}}""",
            """{"req":7}""",
            """{"req":8,"expand":"a","calls":{"subscribe":false}}""",
        ];
        foreach (var message in messages)
        {
            await WebFixture.SendAsync(socket, message);
        }
        var replies = new List<ServerFrame>();
        for (var i = 0; i < messages.Length; i++)
        {
            replies.Add(await WebFixture.ReplyAsync(socket));
        }

        Assert.Equal(
            Enumerable.Range(1, messages.Length).Select(i => (long?)i),
            replies.Select(r => r.Req).Order()
        );
        Assert.All(replies, r => Assert.IsType<MessageFrame>(r));
    }

    [Theory]
    [InlineData(
        """{"req":1,"expand":"abc"}""",
        "unknown archive id: abc (expand takes the hex id from an \"expand <id>\" trailer; it does not read stdin)"
    )]
    [InlineData("""{"req":1,"expand":"ab12cd34ef"}""", "unknown archive id: ab12cd34ef")]
    [InlineData(
        """{"req":1,"doctor":{"action":"plan"}}""",
        "doctor is not available in this build yet"
    )]
    [InlineData(
        """{"req":1,"graph":{"project":"p"}}""",
        "graph is not available in this build yet"
    )]
    [InlineData(
        """{"req":1,"project":{"action":"link","from":"a","to":"b"}}""",
        "project links is not available in this build yet"
    )]
    [InlineData(
        """{"req":1,"calls":{"subscribe":true}}""",
        "live calls is not available in this build yet"
    )]
    [InlineData("""{"req":1,"toggle":{"plugin":"nope","enabled":true}}""", "no such plugin: nope")]
    [InlineData(
        """{"req":1}""",
        "a request names exactly one of toggle, expand, doctor, graph, project, calls"
    )]
    [InlineData("""{"req":1,"toggle":{}}""", "not a request: bad toggle")]
    [InlineData(
        """{"req":1,"toggle":{"plugin":null,"enabled":true}}""",
        "not a request: bad toggle.plugin"
    )]
    [InlineData("""{"req":1,"bogus":1}""", "not a request: bad bogus")]
    [InlineData("""not json""", "not a request")]
    [InlineData("""null""", "not a request")]
    public void Each_message_is_answered_or_refused(string message, string text)
    {
        using var model = new ModelFixture();
        var dispatcher = new WsDispatcher(model.Model(), WebFixture.ExpanderOf(model));

        var frame = dispatcher.Answer(message);

        Assert.Equal(text, Assert.IsType<MessageFrame>(frame).Text);
    }

    [Fact]
    public async Task Expand_returns_the_archived_body_byte_for_byte()
    {
        await using var web = await WebFixture.StartAsync();
        var body =
            "first line\r\nwide: \u00e9\u4e2d\U0001F600\n\ttab and \"quotes\" <tag>\nno newline";
        var id = web.Model.Store.PutArchive("s1", Encoding.UTF8.GetBytes(body));
        using var socket = await web.ConnectAsync();
        _ = await WebFixture.ReceiveAsync(socket);

        await WebFixture.SendAsync(socket, $$"""{"req":4,"expand":"{{id[..8]}}"}""");

        var frame = Assert.IsType<ExpandFrame>(await WebFixture.ReplyAsync(socket));
        Assert.Equal(4, frame.Req);
        Assert.Equal(id[..8], frame.Id);
        Assert.Equal(Encoding.UTF8.GetBytes(body), Encoding.UTF8.GetBytes(frame.Text));
    }

    [Fact]
    public void Without_a_store_an_expand_is_refused()
    {
        using var model = new ModelFixture();

        var frame = new WsDispatcher(model.Model(), null).Answer(
            """{"req":1,"expand":"ab12cd34"}"""
        );

        Assert.Equal(
            "expand needs the store, and none is open",
            Assert.IsType<MessageFrame>(frame).Text
        );
    }

    [Fact]
    public async Task A_toggle_changes_only_that_key_and_the_next_snapshot_shows_it()
    {
        await using var web = await WebFixture.StartAsync();
        var before = await File.ReadAllTextAsync(
            web.Model.UserFile,
            TestContext.Current.CancellationToken
        );
        using var socket = await web.ConnectAsync();
        _ = await WebFixture.ReceiveAsync(socket);

        await WebFixture.SendAsync(
            socket,
            """{"req":9,"toggle":{"plugin":"docs","enabled":false}}"""
        );

        var done = Assert.IsType<DoneFrame>(await WebFixture.ReplyAsync(socket));
        Assert.Equal(9, done.Req);
        Assert.Equal("plugins.docs.enabled = false", done.Text);
        Assert.Equal(
            before.Replace(
                "[plugins.docs]\nenabled = true",
                "[plugins.docs]\nenabled = false",
                StringComparison.Ordinal
            ),
            await File.ReadAllTextAsync(web.Model.UserFile, TestContext.Current.CancellationToken)
        );
        while (true)
        {
            if (
                await WebFixture.ReceiveAsync(socket) is SnapshotFrame { Snapshot: var snapshot }
                && snapshot.Plugins.Single(p =>
                    string.Equals(p.Id, "docs", StringComparison.Ordinal)
                )
                    is { Enabled: false }
            )
            {
                break;
            }
        }
    }

    [Fact]
    public async Task An_oversized_message_is_refused_and_the_socket_stays_open()
    {
        await using var web = await WebFixture.StartAsync();
        using var socket = await web.ConnectAsync();
        _ = await WebFixture.ReceiveAsync(socket);

        await WebFixture.SendAsync(socket, new string(' ', WsDispatcher.MaxMessageBytes + 1));
        var refused = Assert.IsType<MessageFrame>(await WebFixture.ReplyAsync(socket));
        await WebFixture.SendAsync(socket, """{"req":2,"expand":"x"}""");
        var next = await WebFixture.ReplyAsync(socket);

        Assert.Contains("at most", refused.Text, StringComparison.Ordinal);
        Assert.Equal(2, next.Req);
    }

    [Fact]
    public void Every_message_type_round_trips_through_the_shared_context()
    {
        using var model = new ModelFixture();
        var snapshot = model.Model().Build();
        WsRequest[] requests =
        [
            new() { Req = 1, Toggle = new ToggleRequest("docs", true) },
            new() { Req = 2, Expand = "ab12cd34" },
            new() { Doctor = new DoctorRequest("apply", ["hosts"], "tok") },
            new() { Graph = new GraphRequest("p", ["a"], "f", 2, 50, "q") },
            new() { Project = new ProjectRequest("unlink", "a", "b", Both: true) },
            new() { Calls = new CallsRequest(Subscribe: true) },
        ];
        ServerFrame[] frames =
        [
            new SnapshotFrame(snapshot),
            new MessageFrame("m") { Req = 3 },
            new DoneFrame("d") { Req = 4 },
            new ExpandFrame("ab12cd34", "body\n") { Req = 5 },
        ];

        foreach (var request in requests)
        {
            var json = JsonSerializer.Serialize(request, WsJsonContext.Default.WsRequest);
            Assert.Equal(
                json,
                JsonSerializer.Serialize(
                    JsonSerializer.Deserialize(json, WsJsonContext.Default.WsRequest)!,
                    WsJsonContext.Default.WsRequest
                )
            );
        }
        foreach (var frame in frames)
        {
            var json = JsonSerializer.Serialize(frame, WsJsonContext.Default.ServerFrame);
            var back = JsonSerializer.Deserialize(json, WsJsonContext.Default.ServerFrame)!;
            Assert.Equal(frame.GetType(), back.GetType());
            Assert.Equal(json, JsonSerializer.Serialize(back, WsJsonContext.Default.ServerFrame));
        }
    }

    [Fact]
    public void The_wire_types_have_one_copy()
    {
        var wire = typeof(WsJsonContext)
            .Assembly.GetExportedTypes()
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal("Rtok.Web.Contracts", typeof(Snapshot).Assembly.GetName().Name);
        foreach (
            var assembly in new[] { typeof(WebServer).Assembly, typeof(OperatorModel).Assembly }
        )
        {
            Assert.DoesNotContain(assembly.GetTypes(), t => wire.Contains(t.Name));
            Assert.DoesNotContain(
                assembly.GetTypes(),
                t =>
                    t.IsSubclassOf(typeof(System.Text.Json.Serialization.JsonSerializerContext))
                    && t != typeof(WebJsonContext)
            );
        }
    }
}
