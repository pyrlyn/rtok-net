using System.Text.Json.Nodes;

namespace Rtok.Agent.Sdk.Tests;

public sealed class HostFileTests : IDisposable
{
    private readonly Scratch dir = new();

    public void Dispose() => dir.Dispose();

    [Fact]
    public void DryRunAndNoChangesWriteNothing()
    {
        var path = dir["mcp.json"];
        HostFile.Write(new Apply { DryRun = true, Backup = true }, path, "x", "+ x");
        HostFile.Write(new Apply { Backup = true }, path, "x", Apply.NoChanges);
        Assert.Empty(dir.Entries(""));
    }

    [Fact]
    public void BackupKeepsTheOldFileBeforeTheWrite()
    {
        var path = dir.Write("mcp.json", "old");
        HostFile.Write(new Apply { Backup = true }, path, "new", "+ x");
        Assert.Equal("new", dir.Read("mcp.json"));
        Assert.Equal("old", File.ReadAllText(Assert.Single(Directory.GetFiles(dir["_backup"]))));
    }

    [Fact]
    public void WriteCreatesTheParentFolder()
    {
        HostFile.Write(new Apply(), dir["a/b/mcp.json"], "x", "+ x");
        Assert.Equal("x", dir.Read("a/b/mcp.json"));
    }

    [Fact]
    public void WriteGoesThroughASymlinkAndKeepsIt()
    {
        var target = dir.Write("dotfiles/mcp.json", "old");
        var link = dir["mcp.json"];
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception e)
            when (OperatingSystem.IsWindows() && e is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("symlinks need Developer Mode on Windows");
        }
        HostFile.Write(new Apply { Backup = true }, link, "new", "+ x");
        Assert.Equal(target, new FileInfo(link).LinkTarget);
        Assert.Equal("new", File.ReadAllText(target));
        Assert.Single(dir.Entries("_backup"));
    }

    [Fact]
    public void ALiteralTildeIsRefused() =>
        Assert.Throws<ArgumentException>(() =>
            HostFile.Write(new Apply(), "~/.cursor/mcp.json", "x", "+ x")
        );

    [Fact]
    public void MissingOrEmptyJsonIsAnEmptyObject()
    {
        Assert.Equal("{}", HostFile.ReadJson(dir["missing.json"]).ToJsonString());
        Assert.Equal("{}", HostFile.ReadJson(dir.Write("empty.json", " \n")).ToJsonString());
    }

    [Fact]
    public void JsoncIsRefusedWithFileAndLineAndLeftUntouched()
    {
        const string Body = "{\n  // mine\n  \"a\": 1\n}\n";
        var path = dir.Write("settings.json", Body);
        var err = Assert.Throws<InvalidDataException>(() =>
            HostFile.EditJson(new Apply(), path, _ => "+ x")
        );
        Assert.StartsWith($"{path}:2:", err.Message, StringComparison.Ordinal);
        Assert.Contains("The line: // mine", err.Message, StringComparison.Ordinal);
        Assert.Equal(Body, dir.Read("settings.json"));
    }

    [Fact]
    public void JsonIsWrittenWithTwoSpacesAndATrailingNewline()
    {
        var root = new JsonObject
        {
            ["b"] = new JsonObject(),
            ["a"] = new JsonArray(),
            ["s"] = "<ü>",
            ["n"] = new JsonArray(1, 2),
        };
        Assert.Equal(
            "{\n  \"b\": {},\n  \"a\": [],\n  \"s\": \"<ü>\",\n  \"n\": [\n    1,\n    2\n  ]\n}\n",
            HostFile.ToJson(root)
        );
    }

    [Fact]
    public void SlotsOfTheWrongShapeAreReplaced()
    {
        var root = new JsonObject { ["o"] = 1, ["a"] = "x" };
        HostFile.ObjectAt(root, "o")["k"] = 1;
        HostFile.ArrayAt(root, "a").Add(1);
        HostFile.ObjectAt(root, "new");
        Assert.Equal("{\"o\":{\"k\":1},\"a\":[1],\"new\":{}}", root.ToJsonString());
    }

    [Fact]
    public void ANonObjectRootBecomesAnObject()
    {
        var path = dir.Write("mcp.json", "[1]");
        HostFile.EditJson(new Apply(), path, root => (root["a"] = 1) is null ? "" : "+ a");
        Assert.Equal("{\n  \"a\": 1\n}\n", dir.Read("mcp.json"));
    }
}
