using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pyrlyn.Jsonc.Tests;

public sealed class JsoncDocumentTests
{
    private static readonly JsonNode Entry = new JsonObject
    {
        ["command"] = "rtok",
        ["args"] = new JsonArray("mcp"),
    };

    private static string Set(string text, JsoncSegment[] path, JsonNode? value) =>
        JsoncDocument.Parse(text).Set(path, value).Text;

    private static string Remove(string text, params JsoncSegment[] path) =>
        JsoncDocument.Parse(text).Remove(path).Text;

    [Fact]
    public void CommentsAndTrailingCommasParseButJson5StaysRejected()
    {
        Assert.Equal(1, JsoncDocument.Parse("{ /* c */ \"a\": 1, }").Get("a")!.Value.GetInt32());
        foreach (
            var raw in new[]
            {
                ".5",
                "Infinity",
                "NaN",
                "+1",
                "{'a': 1}",
                "\"\\x41\"",
                "{\"a\": 1} 2",
            }
        )
        {
            Assert.Throws<JsoncException>(() => JsoncDocument.Parse(raw));
        }
    }

    [Fact]
    public void AParseErrorNamesFileLineAndColumn()
    {
        var e = Assert.Throws<JsoncException>(() =>
            JsoncDocument.Parse("{\n  \"a\": ,\n}", "settings.json")
        );

        Assert.Equal(("settings.json", 2L), (e.File, e.Line));
        Assert.StartsWith("settings.json:2:", e.Message, StringComparison.Ordinal);
    }

    // Found by the jsonc-edit fuzz target: such a key escaped as InvalidOperationException.
    [Theory]
    [InlineData(
        new byte[]
        {
            (byte)'{',
            (byte)'\n',
            (byte)'"',
            (byte)'m',
            0x95,
            (byte)'"',
            (byte)':',
            (byte)'1',
            (byte)'}',
        }
    )]
    [InlineData(
        new byte[]
        {
            (byte)'{',
            (byte)'\n',
            (byte)'"',
            (byte)'\\',
            (byte)'u',
            (byte)'d',
            (byte)'8',
            (byte)'0',
            (byte)'0',
            (byte)'"',
            (byte)':',
            (byte)'1',
            (byte)'}',
        }
    )]
    public void AKeyOfInvalidUtf8OrALoneSurrogateIsAParseError(byte[] raw)
    {
        var e = Assert.Throws<JsoncException>(() => JsoncDocument.Parse(raw, "settings.json"));

        Assert.Equal(2L, e.Line);
    }

    // Found by the jsonc-edit fuzz target: the document was taken for only comments, and the
    // first edit failed.
    [Theory]
    [InlineData("// a\r>- b\n")]
    [InlineData("// a\u2028\n")]
    [InlineData("/* a")]
    public void CommentsTheReaderRefusesAreAParseError(string raw) =>
        Assert.Throws<JsoncException>(() => JsoncDocument.Parse(raw));

    [Fact]
    public void ALineCommentEndsAtACarriageReturn()
    {
        var doc = JsoncDocument.Parse("// a\r{\"b\": 1}\r\n");

        Assert.Equal(1, doc.Get(["b"])?.GetInt32());
        Assert.Equal("// a\r{\"b\": 2}\r\n", doc.Set(["b"], 2).Text);
    }

    [Fact]
    public void LoadReadsTheBytesAndNamesTheFileInErrors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jsonc-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"a\": [1,]}");
            Assert.Equal("[1,]", JsoncDocument.Load(path).GetRaw("a"));
            File.WriteAllText(path, "{\"a\": }");
            var e = Assert.Throws<JsoncException>(() => JsoncDocument.Load(path));
            Assert.Equal(path, e.File);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetReadsThroughCommentsNestingAndDuplicates()
    {
        var doc = JsoncDocument.Parse(
            "{\n  // \"servers\": fake\n  \"url\": \"https://x/{\\\"a\\\"}\",\n  /* multi\n  \"rtok\": 1 */\n  \"servers\": {\"other\": [1, {\"rtok\": 2}], \"rtok\": 3, \"rtok\": 4}\n}\n"
        );

        Assert.Equal(2, doc.Get("servers", "other", 1, "rtok")!.Value.GetInt32());
        Assert.Equal(4, doc.Get("servers", "rtok")!.Value.GetInt32());
        Assert.Equal("https://x/{\"a\"}", doc.Get("url")!.Value.GetString());
        Assert.False(doc.Contains("rtok"));
        Assert.False(doc.Contains("servers", "other", 2));
        Assert.Null(doc.Get("url", "x"));
    }

    [Fact]
    public void AnEqualValueReturnsTheSameDocument()
    {
        var doc = JsoncDocument.Parse(
            "{\n  \"s\": {\"rtok\": {\"command\": \"rtok\", /* c */ \"args\": [\"mcp\",]}}\n}\n"
        );

        Assert.Same(doc, doc.Set(["s", "rtok"], Entry));
        Assert.Same(doc, doc.Remove("s", "missing"));
        Assert.Same(doc, doc.Remove("t"));
    }

    [Theory]
    [InlineData("true // note\n,")]
    [InlineData("true // a, b\n,")]
    [InlineData("true /* keep */ ,")]
    public void ReplacingAValueKeepsTheCommentAfterIt(string before)
    {
        var raw = $"{{\"s\": {{\"rtok\": {before}\"x\": 1}}}}";

        var after = before["true".Length..];
        Assert.Equal(
            $"{{\"s\": {{\"rtok\": {{\"command\":\"rtok\",\"args\":[\"mcp\"]}}{after}\"x\": 1}}}}",
            Set(raw, ["s", "rtok"], Entry)
        );
    }

    [Fact]
    public void AReplacedValueOnItsOwnLineIsIndentedLikeItsKey()
    {
        var raw = "{\n    \"s\": {\n        \"rtok\": 1, // mine\n        \"x\": 1\n    }\n}\n";

        Assert.Equal(
            "{\n    \"s\": {\n        \"rtok\": {\n            \"command\": \"rtok\",\n            \"args\": [\n                \"mcp\"\n            ]\n        }, // mine\n        \"x\": 1\n    }\n}\n",
            Set(raw, ["s", "rtok"], Entry)
        );
    }

    [Fact]
    public void InsertFollowsTheStyleOfTheLastEntry()
    {
        // After a line comment that ends the last entry's line; a comma added after its value.
        Assert.Equal(
            "{\n  \"a\": 1, // one\n  \"b\": 2\n}\n",
            Set("{\n  \"a\": 1 // one\n}\n", ["b"], 2)
        );
        // A trailing comma stays the style.
        Assert.Equal("{\n  \"a\": 1,\n  \"b\": 2,\n}\n", Set("{\n  \"a\": 1,\n}\n", ["b"], 2));
        // Inline, with and without a trailing comma.
        Assert.Equal("{\"a\": 1, \"b\": 2}", Set("{\"a\": 1}", ["b"], 2));
        Assert.Equal("{\"a\": 1, \"b\": 2,}", Set("{\"a\": 1,}", ["b"], 2));
        // Tabs and CRLF.
        Assert.Equal(
            "{\r\n\t\"a\": {\r\n\t\t\"x\": 1,\r\n\t\t\"y\": {\r\n\t\t\t\"k\": 1\r\n\t\t}\r\n\t}\r\n}\r\n",
            Set(
                "{\r\n\t\"a\": {\r\n\t\t\"x\": 1\r\n\t}\r\n}\r\n",
                ["a", "y"],
                new JsonObject { ["k"] = 1 }
            )
        );
    }

    [Fact]
    public void MissingObjectsOnTheWayAreCreated()
    {
        Assert.Equal(
            "{\n  \"theme\": \"dark\",\n  \"h\": {\n    \"p\": [\n      \"ours\"\n    ]\n  }\n}\n",
            Set("{\n  \"theme\": \"dark\"\n}\n", ["h", "p"], new JsonArray("ours"))
        );
        Assert.Equal(
            "{\n  // only a comment\n  \"a\": {\n    \"b\": 1\n  }\n}\n",
            Set("{\n  // only a comment\n}\n", ["a", "b"], 1)
        );
        Assert.Equal("{\n  \"a\": 1\n}\n", Set("{}\n", ["a"], 1));
        Assert.Equal("{\"a\": [1]}", Set("{\"a\": []}", ["a", 0], 1));
    }

    [Fact]
    public void ADocumentOfOnlyCommentsGetsARootAfterThem()
    {
        var doc = JsoncDocument.Parse("// settings\n");

        Assert.True(doc.IsEmpty);
        Assert.True(JsoncDocument.Parse("").IsEmpty);
        Assert.Equal(
            "// settings\n{\n  \"a\": {\n    \"b\": 1\n  }\n}\n",
            doc.Set(["a", "b"], 1).Text
        );
        Assert.Equal("{\n  \"a\": 1\n}\n", JsoncDocument.Parse("").Set(["a"], 1).Text);
    }

    [Fact]
    public void AValueOfAnotherShapeOnTheWayIsRefused()
    {
        foreach (
            var raw in new[]
            {
                "{\"h\": 1}",
                "{\"h\": {\"p\": {}}}",
                "[1]",
                "{\"h\": {\"p\": [1]}}",
            }
        )
        {
            Assert.Throws<InvalidOperationException>(() => Set(raw, ["h", "p", 2], "x"));
        }
        Assert.Throws<InvalidOperationException>(() => Set("{}", ["h", 0], "x"));
        Assert.Throws<ArgumentException>(() => Remove("{}"));
    }

    [Fact]
    public void KeysNeedingEscapesAndNonAsciiTextRoundTrip()
    {
        var key = """C:\Users\a "quoted"\plugins\rtök""";
        var doc = JsoncDocument
            .Parse("{\n  /* café — ünïcode */\n  \"theme\": \"dark\"\n}\n")
            .Set(["loc", key], true);

        Assert.Contains("/* café — ünïcode */", doc.Text, StringComparison.Ordinal);
        Assert.Contains("rtök", doc.Text, StringComparison.Ordinal);
        Assert.True(doc.Get("loc", key)!.Value.GetBoolean());
        Assert.Equal(
            "{\n  /* café — ünïcode */\n  \"theme\": \"dark\",\n  \"loc\": {}\n}\n",
            doc.Remove("loc", key).Text
        );
    }

    [Fact]
    public void ABomAndTheBytesAroundAnEditStay()
    {
        var bytes = "\uFEFF{\"a\": 1}"u8.ToArray();
        var doc = JsoncDocument.Parse(bytes);

        Assert.Equal(bytes, doc.Utf8.ToArray());
        Assert.Equal("\uFEFF{\"a\": 1, \"b\": 2}"u8.ToArray(), doc.Set(["b"], 2).Utf8.ToArray());
    }

    [Fact]
    public void RemoveKeepsForeignMembersAndComments()
    {
        Assert.Equal(
            "{\n  // mine\n  \"loc\": {\n    // foreign\n    \"/other\": true\n  }\n}\n",
            Remove(
                "{\n  // mine\n  \"loc\": {\n    // foreign\n    \"/other\": true,\n    \"/p\": true\n  }\n}\n",
                "loc",
                "/p"
            )
        );
        Assert.Equal("{\n  \"b\": 2\n}\n", Remove("{\n  \"a\": 1,\n  \"b\": 2\n}\n", "a"));
        Assert.Equal(
            "{\r\n  \"a\": 1\r\n}\r\n",
            Remove("{\r\n  \"a\": 1,\r\n  \"b\": 2\r\n}\r\n", "b")
        );
        Assert.Equal("{}", Remove("{\"a\": {\"x\": 1}}", "a"));
        Assert.Equal(
            "{\"a\": 1, \"a\": 3}",
            Remove("{\"a\": 1, \"b\": 2, \"a\": 3, \"b\": 4}", "b")
        );
    }

    [Theory]
    [InlineData("""{"s":{"rtok":1 /*keep*/,"other":2}}""", """{"s":{ /*keep*/"other":2}}""")]
    [InlineData("{\"s\":{\"rtok\":1 // note\n,\"other\":2}}", "{\"s\":{ // note\n\"other\":2}}")]
    [InlineData("""{"s":{"a":0,"rtok":1 /*k*/,"b":2}}""", """{"s":{"a":0 /*k*/,"b":2}}""")]
    [InlineData("""{"s":{"a":0,"rtok":1 /*k*/}}""", """{"s":{"a":0 /*k*/}}""")]
    [InlineData("""{"s":{"a":0,"rtok":{"x":1} /*k*/,}}""", """{"s":{"a":0 /*k*/,}}""")]
    [InlineData("""{"s":{"rtok":1 , "other":2}}""", """{"s":{ "other":2}}""")]
    [InlineData("""{"s":{"a": 0, "rtok": 1}}""", """{"s":{"a": 0}}""")]
    public void ACommentBetweenARemovedValueAndItsCommaStays(string raw, string want)
    {
        Assert.Equal(want, Remove(raw, "s", "rtok"));
    }

    [Theory]
    [InlineData("""{"a":["ours" /*keep*/,"x"]}""", """{"a":[ /*keep*/"x"]}""", new[] { 0 })]
    [InlineData(
        """{"a":["ours" /*1*/,"ours" /*2*/,"x"]}""",
        """{"a":[ /*1*/ /*2*/"x"]}""",
        new[] { 1, 0 }
    )]
    [InlineData("""{"a":["x","ours" /*k*/]}""", """{"a":["x" /*k*/]}""", new[] { 1 })]
    public void ArrayElementsGoTheSameWay(string raw, string want, int[] indexes)
    {
        var doc = JsoncDocument.Parse(raw);
        foreach (var i in indexes)
        {
            doc = doc.Remove("a", i);
        }

        Assert.Equal(want, doc.Text);
    }

    public static TheoryData<string, string[]> Styles =>
        new()
        {
            { "{\n  // c\n  \"a\": [\"x\", \"y\"],\n  \"b\": 1\n}\n", ["a"] },
            { "{\n  \"a\": [\n    \"x\",\n    \"y\",\n  ]\n}\n", ["a"] },
            { "{\n  \"h\": {\n    \"o\": [1]\n  }\n}\n", ["h", "p"] },
            { "{\n  \"h\": {\n    \"p\": [\n      {\"c\": \"x\"}\n    ]\n  }\n}\n", ["h", "p"] },
            { "{\n  // only a comment\n}\n", ["h", "p"] },
            { "{\"z\": 1}", ["h", "p"] },
            { "{\r\n\t\"h\": {\r\n\t\t\"p\": [ 1 ]\r\n\t}\r\n}\r\n", ["h", "p"] },
        };

    /// <summary>Push an item into a list (made when missing), then pull it and the containers it left empty.</summary>
    [Theory]
    [MemberData(nameof(Styles))]
    public void PushThenPullGivesBackEveryByte(string raw, string[] keys)
    {
        var path = keys.Select(JsoncSegment.Of).ToArray();
        var item = new JsonObject { ["c"] = "ours" };
        var doc = JsoncDocument.Parse(raw);
        var list = doc.Get(path);
        var at = list is { } l ? [.. path, l.GetArrayLength()] : path;
        var pushed = doc.Set(at, list is null ? new JsonArray(item) : item);

        var items = pushed.Get(path)!.Value;
        Assert.Contains(items.EnumerateArray(), e => e.ValueKind == JsonValueKind.Object);
        var back = pushed.Remove([.. path, items.GetArrayLength() - 1]);
        for (
            var depth = path.Length;
            depth > 0 && back.GetRaw(path.AsSpan(0, depth)) is "[]" or "{}";
            depth--
        )
        {
            back = back.Remove(path.AsSpan(0, depth));
        }
        Assert.Equal(raw, back.Text);
    }

    [Fact]
    public void GetRawReturnsTheExactText()
    {
        var doc = JsoncDocument.Parse(Encoding.UTF8.GetBytes("{\"a\": [1, /* two */ 2,]}"));

        Assert.Equal("[1, /* two */ 2,]", doc.GetRaw("a"));
        Assert.Null(doc.GetRaw("b"));
        Assert.Equal(doc.Text, doc.GetRaw());
    }
}
