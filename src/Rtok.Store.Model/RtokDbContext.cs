using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rtok.Store.Model;

/// <summary>
/// The one SQLite file that holds every ledger (rtok D8, D13, D18; README N7). Tables and
/// columns are snake_case. FTS5 tables, their triggers and the <c>notes_topic</c> expression
/// index are not modelled: EF cannot express them, so the migrations add them as
/// <c>migrationBuilder.Sql</c> operations.
/// </summary>
public sealed class RtokDbContext : DbContext
{
    /// <summary>EF marks its whole API as needing reflection and dynamic code. This store runs
    /// on the compiled model and precompiled queries (research.md C.4), which the AOT smoke test
    /// proves; this constructor is where NativeAOT's whole-program analysis reports it.</summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Compiled model and precompiled queries."
    )]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Compiled model and precompiled queries."
    )]
    public RtokDbContext(DbContextOptions<RtokDbContext> options)
        : base(options) { }

    public DbSet<Host> Hosts => Set<Host>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<LlmModel> Models => Set<LlmModel>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<CallRow> Calls => Set<CallRow>();
    public DbSet<CallIo> CallIo => Set<CallIo>();
    public DbSet<HookSession> HookSessions => Set<HookSession>();
    public DbSet<TokenRow> Tokens => Set<TokenRow>();
    public DbSet<LogRow> Logs => Set<LogRow>();
    public DbSet<Usage> Usage => Set<Usage>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<KvEntry> Kv => Set<KvEntry>();
    public DbSet<PluginState> PluginState => Set<PluginState>();
    public DbSet<OtelExport> OtelExport => Set<OtelExport>();
    public DbSet<ArchiveEntry> Archive => Set<ArchiveEntry>();
    public DbSet<ArchiveDecision> ArchiveDecisions => Set<ArchiveDecision>();
    public DbSet<ArchiveSummary> ArchiveSummaries => Set<ArchiveSummary>();
    public DbSet<ReadCacheEntry> ReadCache => Set<ReadCacheEntry>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteVersion> NoteVersions => Set<NoteVersion>();
    public DbSet<NoteFile> NoteFiles => Set<NoteFile>();
    public DbSet<NoteEmbedding> NoteEmbeddings => Set<NoteEmbedding>();
    public DbSet<Observation> Observations => Set<Observation>();
    public DbSet<ObservationFile> ObservationFiles => Set<ObservationFile>();
    public DbSet<Symbol> Symbols => Set<Symbol>();
    public DbSet<SymbolStale> SymbolStale => Set<SymbolStale>();
    public DbSet<Extractor> Extractor => Set<Extractor>();
    public DbSet<FileRank> FileRank => Set<FileRank>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectLink> ProjectLinks => Set<ProjectLink>();
    public DbSet<GraphEvent> GraphEvents => Set<GraphEvent>();
    public DbSet<DocCrate> DocCrates => Set<DocCrate>();
    public DbSet<DocItem> DocItems => Set<DocItem>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<WorktreeClaim> WorktreeClaims => Set<WorktreeClaim>();
    public DbSet<WorktreePending> WorktreePending => Set<WorktreePending>();
    public DbSet<TaskCounter> TaskCounters => Set<TaskCounter>();
    public DbSet<TaskClaim> TaskClaims => Set<TaskClaim>();
    public DbSet<SchemaObject> SchemaObjects => Set<SchemaObject>();
    public DbSet<MigrationHistoryRow> MigrationHistory => Set<MigrationHistoryRow>();

    /// <summary>Host slugs every store starts with; a session of another host has no host row.</summary>
    public static readonly IReadOnlyList<(long Id, string Slug, string Kind)> SeedHosts =
    [
        (1, "claude", "cli"),
        (2, "cursor", "ide"),
        (3, "codex", "cli"),
        (4, "opencode", "cli"),
        (5, "aider", "cli"),
        (6, "other", "other"),
        (7, "pi", "cli"),
    ];

    /// <summary>Providers every store starts with.</summary>
    public static readonly IReadOnlyList<(long Id, string Slug, string Name)> SeedProviders =
    [
        (1, "anthropic", "Anthropic"),
        (2, "openai", "OpenAI"),
        (3, "other", "Other"),
    ];

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Ledger(modelBuilder);
        SessionsAndCalls(modelBuilder);
        CallBodies(modelBuilder);
        Counters(modelBuilder);
        UsageAndMeasurements(modelBuilder);
        ArchiveTables(modelBuilder);
        Memory(modelBuilder);
        Graph(modelBuilder);
        Docs(modelBuilder);
        AgentTables(modelBuilder);

        modelBuilder.Entity<SchemaObject>(e =>
        {
            e.HasNoKey().ToView("sqlite_master");
            e.Property(x => x.Type).HasColumnName("type");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Sql).HasColumnName("sql");
        });
        // EF's own column names; the scripts create the table.
        modelBuilder.Entity<MigrationHistoryRow>(e =>
        {
            e.ToTable("__EFMigrationsHistory", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.MigrationId);
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (
                entity.ClrType == typeof(SchemaObject)
                || entity.ClrType == typeof(MigrationHistoryRow)
            )
            {
                continue;
            }
            // EF's compiled-model generator names a lambda parameter `source`, so no property
            // may be called Source: those map their column by hand and keep it here.
            foreach (var property in entity.GetProperties())
            {
                if (property.FindAnnotation(RelationalAnnotationNames.ColumnName) is null)
                {
                    property.SetColumnName(SnakeCase(property.Name));
                }
            }
        }
    }

    private static void Ledger(ModelBuilder b)
    {
        b.Entity<Host>(e =>
        {
            e.ToTable("hosts");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasData(
                SeedHosts.Select(h => new Host
                {
                    Id = h.Id,
                    Slug = h.Slug,
                    Kind = h.Kind,
                })
            );
        });
        b.Entity<Provider>(e =>
        {
            e.ToTable("providers");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasData(
                SeedProviders.Select(p => new Provider
                {
                    Id = p.Id,
                    Slug = p.Slug,
                    Name = p.Name,
                })
            );
        });
        b.Entity<LlmModel>(e =>
        {
            e.ToTable("models");
            e.HasIndex(x => new { x.ProviderId, x.Slug }).IsUnique();
            e.HasOne<Provider>()
                .WithMany()
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void SessionsAndCalls(ModelBuilder b)
    {
        b.Entity<Session>(e =>
        {
            e.ToTable("sessions");
            e.Property(x => x.SourceName).HasColumnName("source");
            e.HasIndex(x => x.EndSeq);
            e.HasOne<Host>()
                .WithMany()
                .HasForeignKey(x => x.HostId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<CallRow>(e =>
        {
            e.ToTable("calls");
            e.HasIndex(x => new { x.SessionId, x.Ts });
            e.HasIndex(x => new { x.Kind, x.Ts });
            e.HasIndex(x => new { x.Plugin, x.Ts });
            e.HasIndex(x => x.ParentId);
            e.HasOne<Session>()
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<Host>()
                .WithMany()
                .HasForeignKey(x => x.HostId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<Provider>()
                .WithMany()
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<LlmModel>()
                .WithMany()
                .HasForeignKey(x => x.ModelId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<CallRow>()
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void CallBodies(ModelBuilder b)
    {
        b.Entity<CallIo>(e =>
        {
            e.ToTable("call_io");
            e.HasKey(x => x.CallId);
            e.Property(x => x.CallId).ValueGeneratedNever();
            e.HasOne<CallRow>()
                .WithOne()
                .HasForeignKey<CallIo>(x => x.CallId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<ArchiveEntry>()
                .WithMany()
                .HasForeignKey(x => x.RequestArchive)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<ArchiveEntry>()
                .WithMany()
                .HasForeignKey(x => x.ResponseArchive)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<HookSession>()
                .WithMany()
                .HasForeignKey(x => x.HookSessionId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<HookSession>(e =>
        {
            e.ToTable("hook_sessions");
            e.HasIndex(x => x.Fields).IsUnique();
        });
    }

    private static void Counters(ModelBuilder b)
    {
        b.Entity<TokenRow>(e =>
        {
            e.ToTable("tokens");
            e.Property(x => x.SourceName).HasColumnName("source");
            e.HasIndex(x => new { x.CallId, x.Phase });
            e.HasIndex(x => new { x.Plugin, x.Ts });
            e.HasOne<CallRow>()
                .WithMany()
                .HasForeignKey(x => x.CallId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<LogRow>(e =>
        {
            e.ToTable("logs");
            e.Property(x => x.SourceName).HasColumnName("source");
            e.HasIndex(x => x.Ts);
            e.HasIndex(x => new
            {
                x.SourceName,
                x.Name,
                x.Ts,
            });
            e.HasIndex(x => new { x.Session, x.Ts });
            e.HasOne<CallRow>()
                .WithMany()
                .HasForeignKey(x => x.CallId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void UsageAndMeasurements(ModelBuilder b)
    {
        b.Entity<Usage>(e =>
        {
            e.ToTable("usage");
            e.HasIndex(x => new { x.Session, x.Ts });
            e.HasIndex(x => x.CallId);
            e.HasIndex(x => x.ResultKey).IsUnique().HasFilter("result_key IS NOT NULL");
            e.HasOne<CallRow>()
                .WithMany()
                .HasForeignKey(x => x.CallId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<Measurement>(e =>
        {
            e.ToTable("measurements");
            e.HasIndex(x => new { x.Plugin, x.Ts });
            e.HasIndex(x => new { x.Session, x.Ts });
            e.HasIndex(x => x.CallId);
            e.HasIndex(x => x.OnceKey).IsUnique();
            e.HasOne<CallRow>()
                .WithMany()
                .HasForeignKey(x => x.CallId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<KvEntry>(e =>
        {
            e.ToTable("kv");
            e.HasKey(x => x.Key);
        });
        b.Entity<PluginState>(e =>
        {
            e.ToTable("plugin_state");
            e.HasKey(x => new { x.Plugin, x.Key });
        });
        b.Entity<OtelExport>(e =>
        {
            e.ToTable("otel_export");
            e.HasKey(x => x.Stream);
        });
    }

    private static void ArchiveTables(ModelBuilder b)
    {
        b.Entity<ArchiveEntry>(e => e.ToTable("archive"));
        b.Entity<ArchiveDecision>(e =>
        {
            e.ToTable("archive_decisions");
            e.HasKey(x => new { x.Session, x.ToolUseId });
            e.HasIndex(x => x.ArchiveId);
            e.HasOne<ArchiveEntry>()
                .WithMany()
                .HasForeignKey(x => x.ArchiveId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<ArchiveSummary>(e =>
        {
            e.ToTable("archive_summaries");
            e.HasKey(x => new { x.ArchiveId, x.Version });
            e.HasOne<ArchiveEntry>()
                .WithMany()
                .HasForeignKey(x => x.ArchiveId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<ReadCacheEntry>(e =>
        {
            e.ToTable("read_cache");
            e.HasKey(x => new { x.Session, x.Path });
            e.HasOne<ArchiveEntry>()
                .WithMany()
                .HasForeignKey(x => x.ArchiveId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void Memory(ModelBuilder b)
    {
        // notes_topic (COALESCE(project, ''), kind, title) is an expression index: see the migrations.
        b.Entity<Note>(e => e.ToTable("notes"));
        b.Entity<NoteVersion>(e =>
        {
            e.ToTable("note_versions");
            e.HasIndex(x => new { x.NoteId, x.Version }).IsUnique();
            e.HasOne<Note>()
                .WithMany()
                .HasForeignKey(x => x.NoteId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<NoteFile>(e =>
        {
            e.ToTable("note_files");
            e.HasKey(x => new { x.NoteId, x.Path });
            e.HasIndex(x => x.Path);
            e.HasOne<Note>()
                .WithMany()
                .HasForeignKey(x => x.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<NoteEmbedding>(e =>
        {
            e.ToTable("note_embeddings");
            e.HasKey(x => x.NoteId);
            e.Property(x => x.NoteId).ValueGeneratedNever();
            e.HasIndex(x => x.Model);
            e.HasOne<Note>()
                .WithOne()
                .HasForeignKey<NoteEmbedding>(x => x.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Observation>(e =>
        {
            e.ToTable("observations");
            e.HasIndex(x => new { x.Project, x.Ts });
            e.HasIndex(x => new
            {
                x.SessionId,
                x.Dedup,
                x.Ts,
            });
            e.HasIndex(x => x.CallKey).IsUnique();
        });
        b.Entity<ObservationFile>(e =>
        {
            e.ToTable("observation_files");
            e.HasKey(x => new { x.ObservationId, x.Path });
            e.HasIndex(x => x.Path);
            e.HasOne<Observation>()
                .WithMany()
                .HasForeignKey(x => x.ObservationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void Graph(ModelBuilder b)
    {
        b.Entity<Symbol>(e =>
        {
            e.ToTable("symbols");
            e.HasIndex(x => new { x.Root, x.Name });
            e.HasIndex(x => new { x.Root, x.Path });
        });
        b.Entity<SymbolStale>(e =>
        {
            e.ToTable("symbol_stale");
            e.HasKey(x => new { x.Root, x.Path });
        });
        b.Entity<Extractor>(e =>
        {
            e.ToTable("extractor");
            e.HasKey(x => x.Root);
        });
        b.Entity<FileRank>(e =>
        {
            e.ToTable("file_rank");
            e.HasKey(x => x.Root);
        });
        b.Entity<Project>(e =>
        {
            e.ToTable(
                "projects",
                t =>
                {
                    t.HasCheckConstraint(
                        "ck_projects_origin",
                        "origin IN ('manual', 'session', 'worktree', 'mcp', 'reference')"
                    );
                }
            );
            e.HasIndex(x => x.Root).IsUnique();
            // At most one selected project.
            e.HasIndex(x => x.Selected).IsUnique().HasFilter("selected = 1");
        });
        b.Entity<ProjectLink>(e =>
        {
            e.ToTable(
                "project_links",
                t =>
                {
                    t.HasCheckConstraint("ck_project_links_kind", "kind IN ('manual', 'auto')");
                    t.HasCheckConstraint("ck_project_links_not_self", "from_id != to_id");
                }
            );
            e.HasKey(x => new { x.FromId, x.ToId });
            e.HasIndex(x => x.ToId);
            e.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.FromId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ToId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<GraphEvent>(e => e.ToTable("graph_events"));
    }

    private static void Docs(ModelBuilder b)
    {
        b.Entity<DocCrate>(e =>
        {
            e.ToTable("doc_crates");
            e.HasKey(x => new { x.Name, x.Version });
        });
        b.Entity<DocItem>(e =>
        {
            e.ToTable("doc_items");
            e.HasIndex(x => new { x.CrateName, x.Version });
        });
    }

    private static void AgentTables(ModelBuilder b)
    {
        b.Entity<Agent>(e =>
        {
            e.ToTable("agents");
            e.HasIndex(x => new
                {
                    x.HostId,
                    x.HostSessionId,
                    x.ParentKey,
                })
                .IsUnique();
            e.HasIndex(x => x.LastSeen);
            e.HasOne<Host>()
                .WithMany()
                .HasForeignKey(x => x.HostId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasIndex(x => new { x.ToAgent, x.Id });
            e.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(x => x.FromAgent)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(x => x.ToAgent)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<WorktreeClaim>(e =>
        {
            e.ToTable("worktree_claims");
            e.HasKey(x => x.Path);
            e.HasOne<Agent>()
                .WithMany()
                .HasForeignKey(x => x.AgentId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        b.Entity<WorktreePending>(e =>
        {
            e.ToTable("worktree_pending");
            e.HasKey(x => x.Path);
        });
        b.Entity<TaskCounter>(e =>
        {
            e.ToTable("task_counters");
            e.HasKey(x => new { x.Project, x.Parent });
        });
        b.Entity<TaskClaim>(e =>
        {
            e.ToTable("task_claims");
            e.HasKey(x => new { x.Project, x.TaskId });
        });
    }

    /// <summary><c>ServiceTier</c> → <c>service_tier</c>, <c>RequestSha256</c> →
    /// <c>request_sha256</c>.</summary>
    private static string SnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
