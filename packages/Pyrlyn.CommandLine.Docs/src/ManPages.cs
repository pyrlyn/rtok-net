using System.Text;

namespace Pyrlyn.CommandLine.Docs;

/// <summary>roff man pages from a command model: one section-1 page per command.</summary>
public static class ManPages
{
    /// <summary>
    /// Writes <c>&lt;name&gt;.1</c> for the root and <c>&lt;name&gt;-&lt;path&gt;.1</c> for every
    /// other command (<c>rtok-config-get.1</c>). Byte-stable: no date and no version.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The pages, root first.</returns>
    public static IReadOnlyList<GeneratedFile> Render(CommandModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return
        [
            .. model.Commands.Select(c => new GeneratedFile(
                PageName(model.Name, c.Path) + ".1",
                Page(model, c)
            )),
        ];
    }

    /// <summary>The page name of a command: <c>rtok</c>, <c>rtok-config-get</c>.</summary>
    /// <param name="name">The program's name.</param>
    /// <param name="path">The command's path.</param>
    /// <returns>The page name.</returns>
    public static string PageName(string name, string path) =>
        path.Length == 0 ? name : $"{name}-{path.Replace(' ', '-')}";

    private static string Page(CommandModel model, CommandInfo command)
    {
        var page = PageName(model.Name, command.Path);
        var full = command.Path.Length == 0 ? model.Name : $"{model.Name} {command.Path}";
        var sb = new StringBuilder();

        Line(sb, $".TH {page.ToUpperInvariant()} 1");
        Line(sb, ".SH NAME");
        var summary = CommandTree.FirstLine(command.Description);
        Line(sb, summary.Length == 0 ? Escape(page) : $"{Escape(page)} \\- {Escape(summary)}");

        Line(sb, ".SH SYNOPSIS");
        var rest = string.Concat(command.Arguments.Select(a => " " + Placeholder(a.Type)));
        Line(sb, $"\\fB{Escape(full)}\\fR [\\fIoptions\\fR]{rest}");
        foreach (var alias in command.Aliases)
        {
            Line(sb, ".br");
            Line(sb, $"\\fB{Escape($"{model.Name} {alias}")}\\fR [\\fIoptions\\fR]{rest}");
        }

        if (command.Description.Length != 0)
        {
            Line(sb, ".SH DESCRIPTION");
            Text(sb, command.Description);
        }

        if (command.Arguments.Count != 0)
        {
            Line(sb, ".SH ARGUMENTS");
            foreach (var argument in command.Arguments)
            {
                Line(sb, ".TP");
                Line(sb, Placeholder(argument.Type));
                Text(sb, argument.Description);
            }
        }

        Options(sb, command);
        Commands(sb, model, command);
        return sb.ToString();
    }

    private static void Options(StringBuilder sb, CommandInfo command)
    {
        Line(sb, ".SH OPTIONS");
        foreach (var option in command.Options.Concat(CommandTree.Builtins))
        {
            Line(sb, ".TP");
            var names = string.Join(", ", option.Names.Select(n => $"\\fB{Escape(n)}\\fR"));
            Line(sb, option.Type is null ? names : $"{names} \\fI{Escape(option.Type)}\\fR");
            Text(sb, option.Description);
            if (option.Required)
            {
                Line(sb, "Required.");
            }
            else if (option.Default is not null)
            {
                Line(
                    sb,
                    Escape($"Default: {(option.Default.Length == 0 ? "\"\"" : option.Default)}.")
                );
            }
        }
    }

    /// <summary>The commands below this one, each with a pointer to its own page.</summary>
    private static void Commands(StringBuilder sb, CommandModel model, CommandInfo command)
    {
        var prefix = command.Path.Length == 0 ? "" : command.Path + " ";
        var below = model
            .Commands.Where(c =>
                c.Path.Length != 0 && c.Path.StartsWith(prefix, StringComparison.Ordinal)
            )
            .ToList();
        if (below.Count == 0)
        {
            return;
        }
        Line(sb, ".SH COMMANDS");
        foreach (var sub in below)
        {
            Line(sb, ".TP");
            Line(sb, $"\\fB{Escape(sub.Path)}\\fR");
            var hint = CommandTree.FirstLine(sub.Description);
            if (hint.Length != 0)
            {
                Line(sb, Escape(hint));
            }
            Line(sb, $"See \\fB{Escape(PageName(model.Name, sub.Path))}\\fR(1).");
        }
    }

    private static void Line(StringBuilder sb, string text) => sb.Append(text).Append('\n');

    /// <summary>Text as roff lines; a blank line starts a new paragraph. Nothing for empty text.</summary>
    private static void Text(StringBuilder sb, string text)
    {
        if (text.Length == 0)
        {
            return;
        }
        foreach (var line in text.Split('\n'))
        {
            Line(sb, line.Length == 0 ? ".PP" : Escape(line));
        }
    }

    /// <summary><c>string</c> → <c>\fIstring\fR</c>; <c>string?</c> is optional; <c>string[]</c> repeats.</summary>
    private static string Placeholder(string type)
    {
        var optional = type.EndsWith('?');
        var bare = optional ? type[..^1] : type;
        var many = bare.EndsWith("[]", StringComparison.Ordinal);
        bare = many ? bare[..^2] : bare;
        var text = $"\\fI{Escape(bare)}\\fR{(many ? "..." : "")}";
        return optional || many ? $"[{text}]" : text;
    }

    /// <summary>roff-safe text: backslashes and hyphens escaped, a leading dot or quote guarded.</summary>
    private static string Escape(string text)
    {
        var escaped = text.Replace("\\", "\\e", StringComparison.Ordinal)
            .Replace("-", "\\-", StringComparison.Ordinal);
        return escaped.StartsWith('.') || escaped.StartsWith('\'') ? "\\&" + escaped : escaped;
    }
}
