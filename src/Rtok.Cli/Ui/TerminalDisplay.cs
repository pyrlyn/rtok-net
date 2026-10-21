using System.Globalization;
using System.Text;
using Pyrlyn.OperationIcons;
using Rtok.Process;
using Spectre.Console;

namespace Rtok.Cli.Ui;

/// <summary>
/// ProgressRunner's indicators on a terminal (stderr): one line per live indicator in the order
/// they started, so a batch bar keeps the spinner of the command it runs under it. A spinner
/// that learns a total becomes a bar. Each line is cut to the terminal width, and the block is
/// erased before anything else writes (<see cref="Suspend"/>) and when the last one finishes.
/// </summary>
internal sealed class TerminalDisplay : IProgressDisplay, IDisposable
{
    private const int BarWidth = 20;
    private const string Erase = "\e[J";

    private static readonly Spectre.Console.Spinner Dots = Spectre.Console.Spinner.Known.Dots;
    private static readonly IReadOnlyList<string> Frames = Dots.Frames;

    private readonly TextWriter output;
    private readonly Func<int> width;
    private readonly Lazy<bool> color;
    private readonly Lock gate = new();
    private readonly List<Live> live = [];
    private readonly Timer? timer;
    private int drawn;
    private int frame;

    /// <param name="output">The terminal.</param>
    /// <param name="width">Its width in columns.</param>
    /// <param name="color">Whether to colour the spinner and the bar; asked on the first draw.</param>
    /// <param name="animate">Redraw on a timer; tests draw by hand with <see cref="Tick"/>.</param>
    public TerminalDisplay(
        TextWriter output,
        Func<int> width,
        Func<bool> color,
        bool animate = true
    )
    {
        this.output = output;
        this.width = width;
        this.color = new Lazy<bool>(color);
        if (animate)
        {
            timer = new Timer(_ => Tick(), null, Dots.Interval, Dots.Interval);
        }
    }

    /// <summary>A display on stderr when stderr is a terminal, else null.</summary>
    /// <returns>The display, for the caller to install and dispose.</returns>
    public static TerminalDisplay? ForStderr() =>
        Console.IsErrorRedirected
            ? null
            : new TerminalDisplay(Console.Error, ConsoleWidth, () => Lines.ColorOn(stderr: true));

    /// <inheritdoc/>
    public IIndicator Spinner(string label) => Start(label, null, ProgressUnit.Items);

    /// <inheritdoc/>
    public IIndicator Bar(string label, long total, ProgressUnit unit) => Start(label, total, unit);

    /// <inheritdoc/>
    public void Suspend(Action write)
    {
        ArgumentNullException.ThrowIfNull(write);
        lock (gate)
        {
            Clear();
            output.Flush();
            try
            {
                write();
            }
            finally
            {
                Draw();
            }
        }
    }

    /// <summary>Advances the spinners one frame and redraws.</summary>
    public void Tick()
    {
        lock (gate)
        {
            frame = (frame + 1) % Frames.Count;
            if (live.Count > 0)
            {
                Clear();
                Draw();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        timer?.Dispose();
        lock (gate)
        {
            live.Clear();
            Clear();
            output.Flush();
        }
    }

    private static int ConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch (IOException)
        {
            return 80;
        }
    }

    private Live Start(string label, long? total, ProgressUnit unit)
    {
        var indicator = new Live(this, label, total, unit);
        Change(() => live.Add(indicator));
        return indicator;
    }

    private void Change(Action change)
    {
        lock (gate)
        {
            Clear();
            change();
            Draw();
        }
    }

    private void Clear()
    {
        if (drawn > 0)
        {
            output.Write(drawn > 1 ? $"\r\e[{drawn - 1}A{Erase}" : $"\r{Erase}");
            drawn = 0;
        }
    }

    private void Draw()
    {
        var columns = Math.Max(10, width()) - 1;
        for (var i = 0; i < live.Count; i++)
        {
            output.Write(i == 0 ? "" : "\n");
            output.Write(Line(live[i], columns));
        }
        drawn = live.Count;
        output.Flush();
    }

    private string Line(Live indicator, int columns)
    {
        var (head, headColor) = indicator.Total is { } total
            ? ($"[{Bar(indicator.Position, total)}]", Color.Green)
            : (Frames[frame], Color.Teal);
        var text = new StringBuilder(indicator.Label);
        if (indicator.Total is { } of)
        {
            text.Append(' ').Append(Count(indicator.Position, of, indicator.Unit));
        }
        if (indicator.Text is { Length: > 0 } message)
        {
            text.Append(' ').Append(message);
        }
        var rest = Cut(text.ToString(), columns - VerbIcons.Width(head) - 1);
        return $"{(color.Value ? Lines.Paint(head, headColor) : head)} {rest}";
    }

    private static string Bar(long position, long total)
    {
        var filled = total <= 0 ? BarWidth : (int)(Math.Min(position, total) * BarWidth / total);
        return filled >= BarWidth
            ? new string('=', BarWidth)
            : new string('=', filled) + ">" + new string(' ', BarWidth - filled - 1);
    }

    private static string Count(long position, long total, ProgressUnit unit) =>
        unit switch
        {
            ProgressUnit.Percent => string.Create(CultureInfo.InvariantCulture, $"{position}%"),
            ProgressUnit.Bytes => $"{Bytes(position)}/{Bytes(total)}",
            _ => string.Create(CultureInfo.InvariantCulture, $"{position}/{total}"),
        };

    private static string Bytes(long n) =>
        n switch
        {
            < 1024 => string.Create(CultureInfo.InvariantCulture, $"{n} B"),
            < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{n / 1024.0:0.0} KiB"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{n / (1024.0 * 1024):0.0} MiB"),
        };

    /// <summary><paramref name="text"/> cut to <paramref name="columns"/> terminal columns, control characters dropped.</summary>
    private static string Cut(string text, int columns)
    {
        var kept = new StringBuilder();
        var used = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsControl(rune))
            {
                continue;
            }
            var w = VerbIcons.Width(rune.ToString());
            if (used + w > columns)
            {
                break;
            }
            used += w;
            kept.Append(rune.ToString());
        }
        return kept.ToString();
    }

    private sealed class Live(TerminalDisplay display, string label, long? total, ProgressUnit unit)
        : IIndicator
    {
        public string Label => label;

        public long? Total { get; private set; } = total;

        public ProgressUnit Unit { get; private set; } = unit;

        public long Position { get; private set; }

        public string? Text { get; private set; }

        public void Report(ProgressUpdate update)
        {
            ArgumentNullException.ThrowIfNull(update);
            display.Change(() =>
            {
                Position = update.Position;
                Total = update.Total ?? Total;
                Unit = update.Unit;
                Text = update.Message;
            });
        }

        public void Message(string message) => display.Change(() => Text = message);

        public void Suspend(Action write) => display.Suspend(write);

        public void Finish() => display.Change(() => display.live.Remove(this));
    }
}
