using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using Rtok.Cli.Ui;

namespace Rtok.Cli.Shell;

/// <summary>
/// Exit codes for every command: 2 with one error line when the arguments do not parse or
/// validate, 1 with one error line when the command throws. A command's own return value is
/// its exit code otherwise.
/// </summary>
internal sealed class ExitCodeFilter(ConsoleAppFilter next) : ConsoleAppFilter(next)
{
    /// <summary>The exit code for bad arguments.</summary>
    public const int Usage = 2;

    /// <inheritdoc/>
    public override async Task InvokeAsync(
        ConsoleAppContext context,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await Next.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is ArgumentParseFailedException or ValidationException)
        {
            BadArguments(e.Message, $"rtok {context.CommandName} --help");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Lines.Error($"Error: {e.Message}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>Reports bad arguments and sets exit code 2.</summary>
    /// <param name="message">What is wrong.</param>
    /// <param name="help">The help command to suggest.</param>
    public static void BadArguments(string message, string help)
    {
        Lines.Error($"Error: {message}");
        Console.Error.WriteLine($"Run '{help}' for usage.");
        Environment.ExitCode = Usage;
    }
}
