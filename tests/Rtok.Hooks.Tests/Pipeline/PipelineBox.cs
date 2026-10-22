using System.Text;
using Microsoft.Data.Sqlite;
using Rtok.Testing;

namespace Rtok.Hooks.Tests.Pipeline;

/// <summary>A temp rtok home with its own config and store, and the pipeline that runs in it
/// with <see cref="Plugin"/> linked.</summary>
internal sealed class PipelineBox : IDisposable
{
    private readonly TempDir _dir = new("hp-");

    public PipelineBox(string toml = "")
    {
        Directory.CreateDirectory(Project);
        Directory.CreateDirectory(Home);
        // The first event migrates the store: no slow note unless a test asks for one.
        var hook = toml.Contains("[hook]", StringComparison.Ordinal) ? "" : "[hook]\nmax_ms = 0\n";
        File.WriteAllText(
            Path.Join(Home, "config.toml"),
            $"[core]\ndb_path = \"{Db}\"\n{toml}{hook}"
        );
    }

    public string User => _dir.Combine("user");

    public string Home => _dir.Combine("user", ".rtok");

    public string Project => _dir.Combine("project");

    public string Db => Path.Join(Home, "rtok.db");

    public TestPlugin Plugin { get; init; } = new();

    public StringWriter Errors { get; } = new();

    public Dictionary<string, string> Env { get; } = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> ConfigEnv =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HOME"] = User,
            ["RTOK_HOME"] = Home,
        };

    public HookPipeline Pipeline(TimeProvider? time = null, string? executable = null) =>
        new(
            new HookPipelineOptions
            {
                Plugins = () => [Plugin],
                Errors = Errors,
                Environment = name => Env.TryGetValue(name, out var value) ? value : null,
                ConfigEnvironment = ConfigEnv,
                Executable = executable,
                Time = time ?? TimeProvider.System,
            }
        );

    public string Run(HookPipeline pipeline, string hookEvent, string stdin, string host = "") =>
        Run(pipeline, hookEvent, Encoding.UTF8.GetBytes(stdin), host);

    public string Run(HookPipeline pipeline, string hookEvent, byte[] stdin, string host = "")
    {
        using var input = new MemoryStream(stdin);
        return Encoding.UTF8.GetString(pipeline.RunInPlace(hookEvent, host, input, Project));
    }

    public SqliteConnection Connect()
    {
        var connection = new SqliteConnection($"Data Source={Db};Pooling=False");
        connection.Open();
        return connection;
    }

    public long Count(string sql)
    {
        using var connection = Connect();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    public string[] ErrorLines() =>
        Errors.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    public void Dispose() => _dir.Dispose();
}
