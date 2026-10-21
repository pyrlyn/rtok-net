using System.Text;
using System.Text.Json.Nodes;
using CsCheck;

namespace Rtok.Store.Tests;

public sealed class HookFieldsTests
{
    [Fact]
    public void Split_moves_the_session_fields_out_in_a_fixed_order()
    {
        var stdin =
            """{"tool_name":"Bash","cwd":"/w","session_id":"s1","tool_input":{"command":"ls  "}}"""u8.ToArray();

        var (body, fields) = HookFields.Split(stdin)!.Value;

        Assert.Equal(
            """{"tool_name":"Bash","tool_input":{"command":"ls"}}""",
            Encoding.UTF8.GetString(body)
        );
        Assert.Equal("""{"session_id":"s1","cwd":"/w"}""", fields);
    }

    [Theory]
    [InlineData("""{"tool_name":"Bash"}""")]
    [InlineData("""["session_id"]""")]
    [InlineData("not json")]
    [InlineData("\"session_id\"")]
    public void Split_leaves_what_it_cannot_split(string stdin) =>
        Assert.Null(HookFields.Split(Encoding.UTF8.GetBytes(stdin)));

    [Theory]
    [InlineData("""{"a":1}""", null, """{"a":1}""")]
    [InlineData("""{"a":1}""", """{"session_id":"s"}""", """{"session_id":"s","a":1}""")]
    [InlineData("{}", """{"session_id":"s"}""", """{"session_id":"s"}""")]
    [InlineData("""{"a":1}""", "{}", """{"a":1}""")]
    [InlineData("not json", """{"session_id":"s"}""", "not json")]
    public void Rebuild_splices_the_two_objects(string body, string? fields, string want) =>
        Assert.Equal(want, HookFields.Rebuild(body, fields));

    private static readonly string[] Keys =
    [
        "session_id",
        "cwd",
        "agent_id",
        "effort",
        "tool_name",
        "prompt",
        "x",
    ];

    private static readonly string[] Values =
    [
        "a",
        " ",
        "\n",
        "\r\n",
        "\u001b[1m",
        "​",
        "é",
        "\"",
        "<system-reminder>x</system-reminder>",
    ];

    [Fact]
    public void Rebuild_after_split_is_the_cleaned_stdin() =>
        Gen.Dictionary(
                Gen.OneOfConst(Keys),
                Gen.OneOfConst(Values).Array[0, 6].Select(parts => string.Concat(parts))
            )[1, 7]
            .Sample(map =>
            {
                var obj = new JsonObject();
                foreach (var (key, value) in map)
                {
                    obj[key] = value;
                }
                var stdin = Encoding.UTF8.GetBytes(obj.ToJsonString());
                if (HookFields.Split(stdin) is not var (body, fields))
                {
                    return;
                }
                var rebuilt = JsonNode.Parse(
                    HookFields.Rebuild(Encoding.UTF8.GetString(body), fields)
                );
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Sanitize.Body(stdin)), rebuilt));
            });
}
