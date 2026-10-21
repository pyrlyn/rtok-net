using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Rtok.Store.Model.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archive",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    session = table.Column<string>(type: "TEXT", nullable: false),
                    tool = table.Column<string>(type: "TEXT", nullable: true),
                    bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    agent_id = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archive", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "doc_crates",
                columns: table => new
                {
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<string>(type: "TEXT", nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    format_version = table.Column<long>(type: "INTEGER", nullable: false),
                    fetched_unix = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_crates", x => new { x.name, x.version });
                }
            );

            migrationBuilder.CreateTable(
                name: "doc_items",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    crate_name = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    docs = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_items", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "extractor",
                columns: table => new
                {
                    root = table.Column<string>(type: "TEXT", nullable: false),
                    fingerprint = table.Column<string>(type: "TEXT", nullable: false),
                    indexed_at = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extractor", x => x.root);
                }
            );

            migrationBuilder.CreateTable(
                name: "file_rank",
                columns: table => new
                {
                    root = table.Column<string>(type: "TEXT", nullable: false),
                    graph = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_rank", x => x.root);
                }
            );

            migrationBuilder.CreateTable(
                name: "graph_events",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts_ms = table.Column<long>(type: "INTEGER", nullable: false),
                    call = table.Column<string>(type: "TEXT", nullable: false),
                    phase = table.Column<string>(type: "TEXT", nullable: false),
                    session = table.Column<string>(type: "TEXT", nullable: false),
                    tool = table.Column<string>(type: "TEXT", nullable: false),
                    target = table.Column<string>(type: "TEXT", nullable: true),
                    project = table.Column<string>(type: "TEXT", nullable: true),
                    backend = table.Column<string>(type: "TEXT", nullable: true),
                    ok = table.Column<bool>(type: "INTEGER", nullable: false),
                    error = table.Column<string>(type: "TEXT", nullable: true),
                    ms = table.Column<double>(type: "REAL", nullable: true),
                    done = table.Column<long>(type: "INTEGER", nullable: true),
                    total = table.Column<long>(type: "INTEGER", nullable: true),
                    answer_tokens = table.Column<long>(type: "INTEGER", nullable: true),
                    rows_json = table.Column<string>(type: "TEXT", nullable: true),
                    symbols = table.Column<long>(type: "INTEGER", nullable: true),
                    symbols_returned = table.Column<long>(type: "INTEGER", nullable: true),
                    files_touched = table.Column<long>(type: "INTEGER", nullable: true),
                    projects_hit = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_graph_events", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "hook_sessions",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    fields = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hook_sessions", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "hosts",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    slug = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hosts", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "kv",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kv", x => x.key);
                }
            );

            migrationBuilder.CreateTable(
                name: "notes",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    project = table.Column<string>(type: "TEXT", nullable: true),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    body = table.Column<string>(type: "TEXT", nullable: false),
                    retired = table.Column<long>(type: "INTEGER", nullable: true),
                    superseded_by = table.Column<long>(type: "INTEGER", nullable: true),
                    pinned = table.Column<bool>(type: "INTEGER", nullable: false),
                    uses = table.Column<long>(type: "INTEGER", nullable: false),
                    last_used = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notes", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "observations",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    session_id = table.Column<string>(type: "TEXT", nullable: false),
                    project = table.Column<string>(type: "TEXT", nullable: true),
                    obs_type = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    narrative = table.Column<string>(type: "TEXT", nullable: false),
                    dedup = table.Column<string>(type: "TEXT", nullable: false),
                    call_key = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observations", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "otel_export",
                columns: table => new
                {
                    stream = table.Column<string>(type: "TEXT", nullable: false),
                    mark = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otel_export", x => x.stream);
                }
            );

            migrationBuilder.CreateTable(
                name: "plugin_state",
                columns: table => new
                {
                    plugin = table.Column<string>(type: "TEXT", nullable: false),
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_state", x => new { x.plugin, x.key });
                }
            );

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    root = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: true),
                    origin = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    last_used_at = table.Column<long>(type: "INTEGER", nullable: false),
                    selected = table.Column<bool>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.CheckConstraint(
                        "ck_projects_origin",
                        "origin IN ('manual', 'session', 'worktree', 'mcp', 'reference')"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    slug = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "symbol_stale",
                columns: table => new
                {
                    root = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbol_stale", x => new { x.root, x.path });
                }
            );

            migrationBuilder.CreateTable(
                name: "symbols",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    root = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    line = table.Column<long>(type: "INTEGER", nullable: false),
                    end_line = table.Column<long>(type: "INTEGER", nullable: false),
                    start_byte = table.Column<long>(type: "INTEGER", nullable: false),
                    end_byte = table.Column<long>(type: "INTEGER", nullable: false),
                    is_def = table.Column<bool>(type: "INTEGER", nullable: false),
                    scope = table.Column<string>(type: "TEXT", nullable: false),
                    file_sha = table.Column<string>(type: "TEXT", nullable: false),
                    mtime = table.Column<long>(type: "INTEGER", nullable: false),
                    size = table.Column<long>(type: "INTEGER", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false),
                    signature = table.Column<string>(type: "TEXT", nullable: false),
                    doc = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_symbols", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "task_claims",
                columns: table => new
                {
                    project = table.Column<string>(type: "TEXT", nullable: false),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    agent_id = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    since = table.Column<long>(type: "INTEGER", nullable: false),
                    seq = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_claims", x => new { x.project, x.task_id });
                }
            );

            migrationBuilder.CreateTable(
                name: "task_counters",
                columns: table => new
                {
                    project = table.Column<string>(type: "TEXT", nullable: false),
                    parent = table.Column<string>(type: "TEXT", nullable: false),
                    last = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_counters", x => new { x.project, x.parent });
                }
            );

            migrationBuilder.CreateTable(
                name: "worktree_pending",
                columns: table => new
                {
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    task = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worktree_pending", x => x.path);
                }
            );

            migrationBuilder.CreateTable(
                name: "archive_decisions",
                columns: table => new
                {
                    session = table.Column<string>(type: "TEXT", nullable: false),
                    tool_use_id = table.Column<string>(type: "TEXT", nullable: false),
                    archive_id = table.Column<string>(type: "TEXT", nullable: false),
                    pointer = table.Column<string>(type: "TEXT", nullable: false),
                    expanded_ts = table.Column<long>(type: "INTEGER", nullable: true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archive_decisions", x => new { x.session, x.tool_use_id });
                    table.ForeignKey(
                        name: "FK_archive_decisions_archive_archive_id",
                        column: x => x.archive_id,
                        principalTable: "archive",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "archive_summaries",
                columns: table => new
                {
                    archive_id = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<string>(type: "TEXT", nullable: false),
                    summary = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archive_summaries", x => new { x.archive_id, x.version });
                    table.ForeignKey(
                        name: "FK_archive_summaries_archive_archive_id",
                        column: x => x.archive_id,
                        principalTable: "archive",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "read_cache",
                columns: table => new
                {
                    session = table.Column<string>(type: "TEXT", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    archive_id = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_read_cache", x => new { x.session, x.path });
                    table.ForeignKey(
                        name: "FK_read_cache_archive_archive_id",
                        column: x => x.archive_id,
                        principalTable: "archive",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    host_id = table.Column<long>(type: "INTEGER", nullable: false),
                    host_session_id = table.Column<string>(type: "TEXT", nullable: false),
                    parent_key = table.Column<string>(type: "TEXT", nullable: false),
                    parent_id = table.Column<string>(type: "TEXT", nullable: true),
                    cwd = table.Column<string>(type: "TEXT", nullable: true),
                    started_at = table.Column<long>(type: "INTEGER", nullable: false),
                    last_seen = table.Column<long>(type: "INTEGER", nullable: false),
                    ended_at = table.Column<long>(type: "INTEGER", nullable: true),
                    activity = table.Column<string>(type: "TEXT", nullable: true),
                    status_text = table.Column<string>(type: "TEXT", nullable: true),
                    ancestors = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.id);
                    table.ForeignKey(
                        name: "FK_agents_agents_parent_id",
                        column: x => x.parent_id,
                        principalTable: "agents",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_agents_hosts_host_id",
                        column: x => x.host_id,
                        principalTable: "hosts",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    host_id = table.Column<long>(type: "INTEGER", nullable: true),
                    project = table.Column<string>(type: "TEXT", nullable: true),
                    cwd = table.Column<string>(type: "TEXT", nullable: true),
                    source = table.Column<string>(type: "TEXT", nullable: true),
                    started_at = table.Column<long>(type: "INTEGER", nullable: false),
                    ended_at = table.Column<long>(type: "INTEGER", nullable: true),
                    end_seq = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sessions_hosts_host_id",
                        column: x => x.host_id,
                        principalTable: "hosts",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "note_embeddings",
                columns: table => new
                {
                    note_id = table.Column<long>(type: "INTEGER", nullable: false),
                    model = table.Column<string>(type: "TEXT", nullable: false),
                    dims = table.Column<long>(type: "INTEGER", nullable: false),
                    text_hash = table.Column<string>(type: "TEXT", nullable: false),
                    embedded_at = table.Column<long>(type: "INTEGER", nullable: false),
                    vector = table.Column<byte[]>(type: "BLOB", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_embeddings", x => x.note_id);
                    table.ForeignKey(
                        name: "FK_note_embeddings_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "note_files",
                columns: table => new
                {
                    note_id = table.Column<long>(type: "INTEGER", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_files", x => new { x.note_id, x.path });
                    table.ForeignKey(
                        name: "FK_note_files_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "note_versions",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    note_id = table.Column<long>(type: "INTEGER", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    body = table.Column<string>(type: "TEXT", nullable: false),
                    version = table.Column<long>(type: "INTEGER", nullable: false),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_note_versions_notes_note_id",
                        column: x => x.note_id,
                        principalTable: "notes",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "observation_files",
                columns: table => new
                {
                    observation_id = table.Column<long>(type: "INTEGER", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation_files", x => new { x.observation_id, x.path });
                    table.ForeignKey(
                        name: "FK_observation_files_observations_observation_id",
                        column: x => x.observation_id,
                        principalTable: "observations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "project_links",
                columns: table => new
                {
                    from_id = table.Column<long>(type: "INTEGER", nullable: false),
                    to_id = table.Column<long>(type: "INTEGER", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    unlinked = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_links", x => new { x.from_id, x.to_id });
                    table.CheckConstraint("ck_project_links_kind", "kind IN ('manual', 'auto')");
                    table.CheckConstraint("ck_project_links_not_self", "from_id != to_id");
                    table.ForeignKey(
                        name: "FK_project_links_projects_from_id",
                        column: x => x.from_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_project_links_projects_to_id",
                        column: x => x.to_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "models",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    provider_id = table.Column<long>(type: "INTEGER", nullable: false),
                    slug = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_models", x => x.id);
                    table.ForeignKey(
                        name: "FK_models_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    from_agent = table.Column<string>(type: "TEXT", nullable: true),
                    to_agent = table.Column<string>(type: "TEXT", nullable: false),
                    body = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    delivered_at = table.Column<long>(type: "INTEGER", nullable: true),
                    read_at = table.Column<long>(type: "INTEGER", nullable: true),
                    task = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_agents_from_agent",
                        column: x => x.from_agent,
                        principalTable: "agents",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_messages_agents_to_agent",
                        column: x => x.to_agent,
                        principalTable: "agents",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "worktree_claims",
                columns: table => new
                {
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    agent_id = table.Column<string>(type: "TEXT", nullable: false),
                    task = table.Column<string>(type: "TEXT", nullable: false),
                    claimed_at = table.Column<long>(type: "INTEGER", nullable: false),
                    released_at = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worktree_claims", x => x.path);
                    table.ForeignKey(
                        name: "FK_worktree_claims_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "calls",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    session_id = table.Column<string>(type: "TEXT", nullable: false),
                    host_id = table.Column<long>(type: "INTEGER", nullable: true),
                    provider_id = table.Column<long>(type: "INTEGER", nullable: true),
                    model_id = table.Column<long>(type: "INTEGER", nullable: true),
                    plugin = table.Column<string>(type: "TEXT", nullable: true),
                    surface = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    parent_id = table.Column<long>(type: "INTEGER", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: true),
                    ms = table.Column<double>(type: "REAL", nullable: true),
                    ok = table.Column<bool>(type: "INTEGER", nullable: false),
                    error = table.Column<string>(type: "TEXT", nullable: true),
                    service_tier = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calls", x => x.id);
                    table.ForeignKey(
                        name: "FK_calls_calls_parent_id",
                        column: x => x.parent_id,
                        principalTable: "calls",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_calls_hosts_host_id",
                        column: x => x.host_id,
                        principalTable: "hosts",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_calls_models_model_id",
                        column: x => x.model_id,
                        principalTable: "models",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_calls_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_calls_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "sessions",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "call_io",
                columns: table => new
                {
                    call_id = table.Column<long>(type: "INTEGER", nullable: false),
                    request_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    response_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    request_sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    response_sha256 = table.Column<string>(type: "TEXT", nullable: true),
                    request_json = table.Column<string>(type: "TEXT", nullable: true),
                    response_json = table.Column<string>(type: "TEXT", nullable: true),
                    request_archive = table.Column<string>(type: "TEXT", nullable: true),
                    response_archive = table.Column<string>(type: "TEXT", nullable: true),
                    request_raw = table.Column<byte[]>(type: "BLOB", nullable: true),
                    response_raw = table.Column<byte[]>(type: "BLOB", nullable: true),
                    hook_session_id = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_call_io", x => x.call_id);
                    table.ForeignKey(
                        name: "FK_call_io_archive_request_archive",
                        column: x => x.request_archive,
                        principalTable: "archive",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_call_io_archive_response_archive",
                        column: x => x.response_archive,
                        principalTable: "archive",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_call_io_calls_call_id",
                        column: x => x.call_id,
                        principalTable: "calls",
                        principalColumn: "id"
                    );
                    table.ForeignKey(
                        name: "FK_call_io_hook_sessions_hook_session_id",
                        column: x => x.hook_session_id,
                        principalTable: "hook_sessions",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "logs",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    level = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    session = table.Column<string>(type: "TEXT", nullable: true),
                    call_id = table.Column<long>(type: "INTEGER", nullable: true),
                    plugin = table.Column<string>(type: "TEXT", nullable: true),
                    message = table.Column<string>(type: "TEXT", nullable: false),
                    fields = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_logs_calls_call_id",
                        column: x => x.call_id,
                        principalTable: "calls",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "measurements",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    session = table.Column<string>(type: "TEXT", nullable: false),
                    plugin = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    before_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    after_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    est_before = table.Column<long>(type: "INTEGER", nullable: false),
                    est_after = table.Column<long>(type: "INTEGER", nullable: false),
                    ref_id = table.Column<string>(type: "TEXT", nullable: true),
                    call_id = table.Column<long>(type: "INTEGER", nullable: true),
                    once_key = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_measurements", x => x.id);
                    table.ForeignKey(
                        name: "FK_measurements_calls_call_id",
                        column: x => x.call_id,
                        principalTable: "calls",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "tokens",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    call_id = table.Column<long>(type: "INTEGER", nullable: false),
                    plugin = table.Column<string>(type: "TEXT", nullable: true),
                    phase = table.Column<string>(type: "TEXT", nullable: false),
                    source = table.Column<string>(type: "TEXT", nullable: false),
                    tokens = table.Column<long>(type: "INTEGER", nullable: false),
                    bytes = table.Column<long>(type: "INTEGER", nullable: true),
                    input = table.Column<long>(type: "INTEGER", nullable: true),
                    output = table.Column<long>(type: "INTEGER", nullable: true),
                    cache_create = table.Column<long>(type: "INTEGER", nullable: true),
                    cache_read = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_tokens_calls_call_id",
                        column: x => x.call_id,
                        principalTable: "calls",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "usage",
                columns: table => new
                {
                    id = table
                        .Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ts = table.Column<long>(type: "INTEGER", nullable: false),
                    session = table.Column<string>(type: "TEXT", nullable: false),
                    model = table.Column<string>(type: "TEXT", nullable: true),
                    input = table.Column<long>(type: "INTEGER", nullable: false),
                    cache_create = table.Column<long>(type: "INTEGER", nullable: false),
                    cache_read = table.Column<long>(type: "INTEGER", nullable: false),
                    output = table.Column<long>(type: "INTEGER", nullable: false),
                    call_id = table.Column<long>(type: "INTEGER", nullable: true),
                    api = table.Column<string>(type: "TEXT", nullable: false),
                    result_key = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage", x => x.id);
                    table.ForeignKey(
                        name: "FK_usage_calls_call_id",
                        column: x => x.call_id,
                        principalTable: "calls",
                        principalColumn: "id"
                    );
                }
            );

            migrationBuilder.InsertData(
                table: "hosts",
                columns: new[] { "id", "created_at", "kind", "slug" },
                values: new object[,]
                {
                    { 1L, 0L, "cli", "claude" },
                    { 2L, 0L, "ide", "cursor" },
                    { 3L, 0L, "cli", "codex" },
                    { 4L, 0L, "cli", "opencode" },
                    { 5L, 0L, "cli", "aider" },
                    { 6L, 0L, "other", "other" },
                    { 7L, 0L, "cli", "pi" },
                }
            );

            migrationBuilder.InsertData(
                table: "providers",
                columns: new[] { "id", "created_at", "name", "slug" },
                values: new object[,]
                {
                    { 1L, 0L, "Anthropic", "anthropic" },
                    { 2L, 0L, "OpenAI", "openai" },
                    { 3L, 0L, "Other", "other" },
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_agents_host_id_host_session_id_parent_key",
                table: "agents",
                columns: new[] { "host_id", "host_session_id", "parent_key" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_agents_last_seen",
                table: "agents",
                column: "last_seen"
            );

            migrationBuilder.CreateIndex(
                name: "IX_agents_parent_id",
                table: "agents",
                column: "parent_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_archive_decisions_archive_id",
                table: "archive_decisions",
                column: "archive_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_call_io_hook_session_id",
                table: "call_io",
                column: "hook_session_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_call_io_request_archive",
                table: "call_io",
                column: "request_archive"
            );

            migrationBuilder.CreateIndex(
                name: "IX_call_io_response_archive",
                table: "call_io",
                column: "response_archive"
            );

            migrationBuilder.CreateIndex(
                name: "IX_calls_host_id",
                table: "calls",
                column: "host_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_calls_kind_ts",
                table: "calls",
                columns: new[] { "kind", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_calls_model_id",
                table: "calls",
                column: "model_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_calls_parent_id",
                table: "calls",
                column: "parent_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_calls_plugin_ts",
                table: "calls",
                columns: new[] { "plugin", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_calls_provider_id",
                table: "calls",
                column: "provider_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_calls_session_id_ts",
                table: "calls",
                columns: new[] { "session_id", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_doc_items_crate_name_version",
                table: "doc_items",
                columns: new[] { "crate_name", "version" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_hook_sessions_fields",
                table: "hook_sessions",
                column: "fields",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_hosts_slug",
                table: "hosts",
                column: "slug",
                unique: true
            );

            migrationBuilder.CreateIndex(name: "IX_logs_call_id", table: "logs", column: "call_id");

            migrationBuilder.CreateIndex(
                name: "IX_logs_session_ts",
                table: "logs",
                columns: new[] { "session", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_logs_source_name_ts",
                table: "logs",
                columns: new[] { "source", "name", "ts" }
            );

            migrationBuilder.CreateIndex(name: "IX_logs_ts", table: "logs", column: "ts");

            migrationBuilder.CreateIndex(
                name: "IX_measurements_call_id",
                table: "measurements",
                column: "call_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_measurements_once_key",
                table: "measurements",
                column: "once_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_measurements_plugin_ts",
                table: "measurements",
                columns: new[] { "plugin", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_measurements_session_ts",
                table: "measurements",
                columns: new[] { "session", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_messages_from_agent",
                table: "messages",
                column: "from_agent"
            );

            migrationBuilder.CreateIndex(
                name: "IX_messages_to_agent_id",
                table: "messages",
                columns: new[] { "to_agent", "id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_models_provider_id_slug",
                table: "models",
                columns: new[] { "provider_id", "slug" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_note_embeddings_model",
                table: "note_embeddings",
                column: "model"
            );

            migrationBuilder.CreateIndex(
                name: "IX_note_files_path",
                table: "note_files",
                column: "path"
            );

            migrationBuilder.CreateIndex(
                name: "IX_note_versions_note_id_version",
                table: "note_versions",
                columns: new[] { "note_id", "version" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_observation_files_path",
                table: "observation_files",
                column: "path"
            );

            migrationBuilder.CreateIndex(
                name: "IX_observations_call_key",
                table: "observations",
                column: "call_key",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_observations_project_ts",
                table: "observations",
                columns: new[] { "project", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_observations_session_id_dedup_ts",
                table: "observations",
                columns: new[] { "session_id", "dedup", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_project_links_to_id",
                table: "project_links",
                column: "to_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_projects_root",
                table: "projects",
                column: "root",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_projects_selected",
                table: "projects",
                column: "selected",
                unique: true,
                filter: "selected = 1"
            );

            migrationBuilder.CreateIndex(
                name: "IX_providers_slug",
                table: "providers",
                column: "slug",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_read_cache_archive_id",
                table: "read_cache",
                column: "archive_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_sessions_end_seq",
                table: "sessions",
                column: "end_seq"
            );

            migrationBuilder.CreateIndex(
                name: "IX_sessions_host_id",
                table: "sessions",
                column: "host_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_symbols_root_name",
                table: "symbols",
                columns: new[] { "root", "name" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_symbols_root_path",
                table: "symbols",
                columns: new[] { "root", "path" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_tokens_call_id_phase",
                table: "tokens",
                columns: new[] { "call_id", "phase" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_tokens_plugin_ts",
                table: "tokens",
                columns: new[] { "plugin", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_usage_call_id",
                table: "usage",
                column: "call_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_usage_result_key",
                table: "usage",
                column: "result_key",
                unique: true,
                filter: "result_key IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "IX_usage_session_ts",
                table: "usage",
                columns: new[] { "session", "ts" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_worktree_claims_agent_id",
                table: "worktree_claims",
                column: "agent_id"
            );

            // EF cannot model FTS5 tables, their sync triggers or an expression index (T8).
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX notes_topic ON notes (COALESCE(project, ''), kind, title);
                """
            );
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE notes_fts USING fts5 (title, body, content = 'notes', content_rowid = 'id');
                CREATE TRIGGER notes_ai AFTER INSERT ON notes BEGIN
                    INSERT INTO notes_fts (rowid, title, body) VALUES (new.id, new.title, new.body);
                END;
                CREATE TRIGGER notes_ad AFTER DELETE ON notes BEGIN
                    INSERT INTO notes_fts (notes_fts, rowid, title, body) VALUES ('delete', old.id, old.title, old.body);
                END;
                CREATE TRIGGER notes_au AFTER UPDATE ON notes BEGIN
                    INSERT INTO notes_fts (notes_fts, rowid, title, body) VALUES ('delete', old.id, old.title, old.body);
                    INSERT INTO notes_fts (rowid, title, body) VALUES (new.id, new.title, new.body);
                END;
                """
            );
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE observations_fts USING fts5 (title, narrative, content = 'observations', content_rowid = 'id');
                CREATE TRIGGER observations_ai AFTER INSERT ON observations BEGIN
                    INSERT INTO observations_fts (rowid, title, narrative) VALUES (new.id, new.title, new.narrative);
                END;
                CREATE TRIGGER observations_ad AFTER DELETE ON observations BEGIN
                    INSERT INTO observations_fts (observations_fts, rowid, title, narrative) VALUES ('delete', old.id, old.title, old.narrative);
                END;
                CREATE TRIGGER observations_au AFTER UPDATE ON observations BEGIN
                    INSERT INTO observations_fts (observations_fts, rowid, title, narrative) VALUES ('delete', old.id, old.title, old.narrative);
                    INSERT INTO observations_fts (rowid, title, narrative) VALUES (new.id, new.title, new.narrative);
                END;
                """
            );
            // Only named definitions are searchable.
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE symbols_fts USING fts5 (name, signature, doc, content = 'symbols', content_rowid = 'id');
                CREATE TRIGGER symbols_ai AFTER INSERT ON symbols WHEN new.is_def = 1 AND new.name != '' BEGIN
                    INSERT INTO symbols_fts (rowid, name, signature, doc) VALUES (new.id, new.name, new.signature, new.doc);
                END;
                CREATE TRIGGER symbols_ad AFTER DELETE ON symbols WHEN old.is_def = 1 AND old.name != '' BEGIN
                    INSERT INTO symbols_fts (symbols_fts, rowid, name, signature, doc) VALUES ('delete', old.id, old.name, old.signature, old.doc);
                END;
                CREATE TRIGGER symbols_au AFTER UPDATE ON symbols BEGIN
                    INSERT INTO symbols_fts (symbols_fts, rowid, name, signature, doc)
                    SELECT 'delete', old.id, old.name, old.signature, old.doc WHERE old.is_def = 1 AND old.name != '';
                    INSERT INTO symbols_fts (rowid, name, signature, doc)
                    SELECT new.id, new.name, new.signature, new.doc WHERE new.is_def = 1 AND new.name != '';
                END;
                """
            );
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE doc_items_fts USING fts5 (path, docs, content = 'doc_items', content_rowid = 'id');
                CREATE TRIGGER doc_items_ai AFTER INSERT ON doc_items BEGIN
                    INSERT INTO doc_items_fts (rowid, path, docs) VALUES (new.id, new.path, new.docs);
                END;
                CREATE TRIGGER doc_items_ad AFTER DELETE ON doc_items BEGIN
                    INSERT INTO doc_items_fts (doc_items_fts, rowid, path, docs) VALUES ('delete', old.id, old.path, old.docs);
                END;
                CREATE TRIGGER doc_items_au AFTER UPDATE ON doc_items BEGIN
                    INSERT INTO doc_items_fts (doc_items_fts, rowid, path, docs) VALUES ('delete', old.id, old.path, old.docs);
                    INSERT INTO doc_items_fts (rowid, path, docs) VALUES (new.id, new.path, new.docs);
                END;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TABLE doc_items_fts;
                DROP TABLE symbols_fts;
                DROP TABLE observations_fts;
                DROP TABLE notes_fts;
                DROP INDEX notes_topic;
                """
            );
            migrationBuilder.DropTable(name: "archive_decisions");

            migrationBuilder.DropTable(name: "archive_summaries");

            migrationBuilder.DropTable(name: "call_io");

            migrationBuilder.DropTable(name: "doc_crates");

            migrationBuilder.DropTable(name: "doc_items");

            migrationBuilder.DropTable(name: "extractor");

            migrationBuilder.DropTable(name: "file_rank");

            migrationBuilder.DropTable(name: "graph_events");

            migrationBuilder.DropTable(name: "kv");

            migrationBuilder.DropTable(name: "logs");

            migrationBuilder.DropTable(name: "measurements");

            migrationBuilder.DropTable(name: "messages");

            migrationBuilder.DropTable(name: "note_embeddings");

            migrationBuilder.DropTable(name: "note_files");

            migrationBuilder.DropTable(name: "note_versions");

            migrationBuilder.DropTable(name: "observation_files");

            migrationBuilder.DropTable(name: "otel_export");

            migrationBuilder.DropTable(name: "plugin_state");

            migrationBuilder.DropTable(name: "project_links");

            migrationBuilder.DropTable(name: "read_cache");

            migrationBuilder.DropTable(name: "symbol_stale");

            migrationBuilder.DropTable(name: "symbols");

            migrationBuilder.DropTable(name: "task_claims");

            migrationBuilder.DropTable(name: "task_counters");

            migrationBuilder.DropTable(name: "tokens");

            migrationBuilder.DropTable(name: "usage");

            migrationBuilder.DropTable(name: "worktree_claims");

            migrationBuilder.DropTable(name: "worktree_pending");

            migrationBuilder.DropTable(name: "hook_sessions");

            migrationBuilder.DropTable(name: "notes");

            migrationBuilder.DropTable(name: "observations");

            migrationBuilder.DropTable(name: "projects");

            migrationBuilder.DropTable(name: "archive");

            migrationBuilder.DropTable(name: "calls");

            migrationBuilder.DropTable(name: "agents");

            migrationBuilder.DropTable(name: "models");

            migrationBuilder.DropTable(name: "sessions");

            migrationBuilder.DropTable(name: "providers");

            migrationBuilder.DropTable(name: "hosts");
        }
    }
}
