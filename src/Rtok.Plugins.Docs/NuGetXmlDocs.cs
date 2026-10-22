using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Rtok.Store;

namespace Rtok.Plugins.Docs;

/// <summary>
/// A <c>.nupkg</c>'s XML documentation: the <c>lib/</c> or <c>ref/</c> <c>.xml</c> file with the
/// most members (ties by path), one item per documented member.
/// </summary>
public static class NuGetXmlDocs
{
    /// <summary>An XML docs file larger than this, unpacked, is skipped.</summary>
    public const long MaxXmlBytes = 64 << 20;

    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaxXmlBytes,
        IgnoreComments = true,
    };

    /// <summary>
    /// The items of the package in <paramref name="nupkg"/>: each member's id without its
    /// <c>T:</c>-style prefix as the path, the prefix as the kind (<c>type</c>, <c>method</c>, …),
    /// and its doc elements as text. No docs file gives no items.
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes are not a zip archive.</exception>
    public static IReadOnlyList<DocEntry> Read(Stream nupkg)
    {
        using var zip = new ZipArchive(nupkg, ZipArchiveMode.Read);
        IReadOnlyList<DocEntry> best = [];
        string? bestPath = null;
        var candidates = zip
            .Entries.Where(e =>
                (
                    e.FullName.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                    || e.FullName.StartsWith("ref/", StringComparison.OrdinalIgnoreCase)
                )
                && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                && e.Length <= MaxXmlBytes
            )
            .OrderBy(e => e.FullName, StringComparer.Ordinal);
        foreach (var entry in candidates)
        {
            IReadOnlyList<DocEntry> items;
            try
            {
                using var stream = entry.Open();
                items = Members(stream);
            }
            catch (XmlException)
            {
                continue;
            }
            if (bestPath is null || items.Count > best.Count)
            {
                (best, bestPath) = (items, entry.FullName);
            }
        }
        return best;
    }

    /// <summary>The documented members of one XML docs file.</summary>
    /// <exception cref="XmlException">The text is not XML, or has a DTD.</exception>
    public static IReadOnlyList<DocEntry> Members(Stream xml)
    {
        using var reader = XmlReader.Create(xml, Settings);
        var doc = XDocument.Load(reader);
        var items = new List<DocEntry>();
        if (doc.Root is not { Name.LocalName: "doc" } root)
        {
            return items;
        }
        foreach (var member in root.Elements("members").Elements("member"))
        {
            var name = (string?)member.Attribute("name") ?? "";
            var text = Text(member);
            if (name.Length < 3 || name[1] != ':' || text.Length == 0)
            {
                continue;
            }
            items.Add(new DocEntry(name[2..], Kind(name[0]), text));
        }
        return items;
    }

    private static string Kind(char prefix) =>
        prefix switch
        {
            'T' => "type",
            'M' => "method",
            'P' => "property",
            'F' => "field",
            'E' => "event",
            'N' => "namespace",
            _ => "member",
        };

    // summary first and bare; every other element as "<name> [name attr]: text", one per line.
    private static string Text(XElement member)
    {
        var lines = new List<string>();
        foreach (var part in member.Elements().OrderBy(e => IsSummary(e.Name.LocalName) ? 0 : 1))
        {
            var body = Flat(part);
            if (body.Length == 0)
            {
                continue;
            }
            var label = part.Name.LocalName;
            if ((string?)part.Attribute("name") is { Length: > 0 } arg)
            {
                label += " " + arg;
            }
            lines.Add(IsSummary(label) ? body : label + ": " + body);
        }
        return string.Join('\n', lines);
    }

    private static bool IsSummary(string name) =>
        string.Equals(name, "summary", StringComparison.Ordinal);

    // Text with <see cref="T:X"/> as X and whitespace collapsed.
    private static string Flat(XElement element)
    {
        var sb = new StringBuilder();
        foreach (var node in element.DescendantNodes())
        {
            if (node is XText t)
            {
                sb.Append(t.Value);
            }
            else if (
                node is XElement { IsEmpty: true } e
                && (
                    (string?)e.Attribute("cref")
                    ?? (string?)e.Attribute("langword")
                    ?? (string?)e.Attribute("name")
                )
                    is { Length: > 0 } reference
            )
            {
                sb.Append(reference.Length > 2 && reference[1] == ':' ? reference[2..] : reference);
            }
        }
        return string.Join(
            ' ',
            sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
    }
}
