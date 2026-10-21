using System.Diagnostics;

namespace Pyrlyn.CommandLine.Docs.Tests;

/// <summary>The scripts in the shells on PATH: their syntax, and bash's answers. A missing shell skips.</summary>
public sealed class ShellTests : IDisposable
{
    private static readonly Lazy<IReadOnlyList<GeneratedFile>> Scripts = new(() =>
        CompletionScripts.Render(SampleApp.Read())
    );

    private readonly string _dir = Directory.CreateTempSubdirectory("pyrlyn-cldocs-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("bash", "sample.bash", "-n")]
    [InlineData("zsh", "_sample", "-n")]
    [InlineData("fish", "sample.fish", "--no-execute")]
    public void TheShellAcceptsTheSyntax(string shell, string script, string flag)
    {
        var path = Write(script);
        var (code, output) = Run(shell, [flag, path]);
        Assert.True(code == 0, output);
    }

    [Fact]
    public void PowerShellParsesTheScript()
    {
        var path = Write("sample.ps1");
        // -Command takes the rest of the line as script text, so the path goes into it.
        var quoted = path.Replace("'", "''", StringComparison.Ordinal);
        var (code, output) = Run(
            "pwsh",
            [
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                $"$errors = $null; [void][System.Management.Automation.Language.Parser]::ParseFile('{quoted}', [ref]$null, [ref]$errors); $errors | ForEach-Object {{ $_.ToString() }}; exit $errors.Count",
            ]
        );
        Assert.True(code == 0, output);
    }

    [Fact]
    public void LuaAcceptsTheClinkScriptSyntax()
    {
        var (code, output) = Run("luac", ["-p", Write("sample.lua")], probe: "-v");
        Assert.True(code == 0, output);
    }

    [Theory]
    [InlineData("sample ", "config\nls\nput\nrm\nstats\nworktree")]
    [InlineData("sample con", "config")]
    [InlineData("sample config ", "get\nset")]
    [InlineData("sample worktree r", "remove")]
    [InlineData("sample -v worktree ", "list\nremove")]
    [InlineData("sample ls --l", "--limit\n--lines")]
    [InlineData("sample worktree list -n ", "")]
    [InlineData("sample config get --scope config ", "")]
    [InlineData("sample stats --b", "--by")]
    [InlineData("sample --d", "--dry-run")]
    public void BashCompletesPathsAndOptions(string line, string expected)
    {
        var path = Write("sample.bash");
        // COMP_WORDS as bash splits the line; a trailing space starts an empty word.
        var words = line.Split(' ');
        var script =
            $"source '{path}'; COMP_WORDS=({string.Join(' ', words.Select(w => $"'{w}'"))}); "
            + $"COMP_CWORD={words.Length - 1}; _sample; "
            + "printf '%s\\n' \"${COMPREPLY[@]}\" | LC_ALL=C sort";
        var (code, output) = Run("bash", ["--norc", "--noprofile", "-c", script]);
        Assert.True(code == 0, output);
        Assert.Equal(expected, output.TrimEnd('\n'));
    }

    [Theory]
    [InlineData("sample ", "config ls put rm stats worktree")]
    [InlineData("sample config ", "get set")]
    [InlineData("sample ls --l", "--limit --lines")]
    [InlineData("sample -v worktree r", "remove")]
    [InlineData("sample put --f", "--force")]
    public void FishCompletesPathsAndOptions(string line, string expected)
    {
        var path = Write("sample.fish");
        var (code, output) = Run(
            "fish",
            [
                "--no-config",
                "-c",
                $"source '{path}'; complete -C '{line}' | string replace -r '\\t.*' ''",
            ]
        );
        Assert.True(code == 0, output);
        Assert.Equal(
            expected,
            string.Join(' ', output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        );
    }

    [Theory]
    [InlineData("sample ", "config put stats worktree ls rm")]
    [InlineData("sample config ", "get set")]
    [InlineData("sample -v worktree r", "list remove")]
    [InlineData("sample worktree list -n ", "_files")]
    [InlineData("sample config get k", "_files")]
    public void ZshCompletesPathsAndOptions(string line, string expected)
    {
        // Without a terminal there is no completion system: _describe and _files print what
        // they were given (the prefix filter is theirs), and compdef does nothing.
        var path = Write("_sample");
        var script =
            "compdef() { :; }; _describe() { print -l -- \"${(@P)4%%:*}\"; }; _files() { print _files; }; "
            + $"source '{path}'; words=(${{(s: :)LINE}}); [[ $LINE == *' ' ]] && words+=(''); "
            + "CURRENT=${#words}; PREFIX=${words[-1]}; _sample";
        var (code, output) = Run("zsh", ["-f", "-c", $"LINE='{line}'; {script}"]);
        Assert.True(code == 0, output);
        Assert.Equal(
            expected,
            string.Join(' ', output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        );
    }

    private string Write(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(
            path,
            Scripts.Value.Single(f => string.Equals(f.Name, name, StringComparison.Ordinal)).Content
        );
        return path;
    }

    private static (int Code, string Output) Run(
        string shell,
        IReadOnlyList<string> args,
        string probe = "--version"
    )
    {
        // A shell that cannot even print its version (a version-manager shim with no version
        // set) counts as missing.
        if (Start(shell, [probe]) is not { } version || version.Code != 0)
        {
            Assert.Skip($"{shell} is not on PATH");
        }
        return Start(shell, args)!.Value;
    }

    private static (int Code, string Output)? Start(string shell, IReadOnlyList<string> args)
    {
        var start = new ProcessStartInfo(shell)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }
        Process process;
        try
        {
            process = Process.Start(start)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
        using (process)
        {
            var stderr = process.StandardError.ReadToEndAsync(
                TestContext.Current.CancellationToken
            );
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout + stderr.Result);
        }
    }
}
