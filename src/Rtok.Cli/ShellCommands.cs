using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Pyrlyn.CommandLine.Docs;
using Rtok.Cli.Shell;
using Rtok.Cli.Ui;

namespace Rtok.Cli;

/// <summary>Shell completions and man pages, generated from the command model.</summary>
internal sealed class ShellCommands
{
    private readonly TextWriter _stdout = Console.Out;

    /// <summary>Print or install shell completions (bash, zsh, fish, powershell; clink for Windows cmd).</summary>
    /// <param name="shell">The shell: bash, zsh, fish, powershell or clink (with --install or --uninstall: default $SHELL).</param>
    /// <param name="install">Write the script to the shell's per-user completions directory.</param>
    /// <param name="uninstall">Remove what --install wrote.</param>
    [Command("completions")]
    public void Completions(
        [Argument] string? shell = null,
        bool install = false,
        bool uninstall = false
    )
    {
        if (shell is not null && !CommandDocs.Shells.Contains(shell, StringComparer.Ordinal))
        {
            throw new ValidationException(
                $"unknown shell '{shell}'; name one of: {string.Join(", ", CommandDocs.Shells)}"
            );
        }
        if (install && uninstall)
        {
            throw new ValidationException("--install and --uninstall cannot be used together");
        }
        if (!install && !uninstall)
        {
            _stdout.Write(
                CommandDocs.Script(
                    shell
                        ?? throw new ValidationException(
                            "name a shell to print its script, or pass --install to write one for $SHELL"
                        )
                )
            );
            return;
        }
        var places = CompletionPlaces.FromEnvironment();
        var picked = places.Pick(shell);
        var reports = install
            ? CompletionInstaller.Install(picked, CommandDocs.Script(picked), places)
            : CompletionInstaller.Uninstall(picked, places);
        foreach (var report in reports)
        {
            if (report.Verb is "wrote" or "removed" or "updated")
            {
                Lines.Success(report.ToString(), install ? "install" : "uninstall");
            }
            else
            {
                Lines.Info(report.ToString());
            }
        }
    }

    /// <summary>Print the man page (roff), or write every page with --dir.</summary>
    /// <param name="dir">Write rtok.1 and a page for every subcommand into this directory, and print their paths.</param>
    [Command("man")]
    public void Man(string? dir = null)
    {
        var pages = ManPages.Render(CommandDocs.Model);
        if (dir is null)
        {
            // Root first: rtok.1.
            _stdout.Write(pages[0].Content);
            return;
        }
        Directory.CreateDirectory(dir);
        foreach (var page in pages)
        {
            var path = Path.Join(dir, page.Name);
            File.WriteAllText(path, page.Content);
            _stdout.WriteLine(path);
        }
    }
}
