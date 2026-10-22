using System.Text;
using Rtok.Store.Model;

namespace Rtok.Store;

public sealed partial class RtokStore
{
    /// <summary>Saves a call's request and response. The request is cleaned first unless
    /// <see cref="StoreOptions.StoreRaw"/> (rtok T431), so the sha, size and archive file all
    /// describe the saved bytes. A body over <see cref="StoreOptions.CallIoInlineBytes"/> goes
    /// to <see cref="StoreOptions.ArchiveDir"/>, or keeps only its size without one.</summary>
    public void InsertCallIo(long callId, byte[]? request, byte[]? response) =>
        WriteCallIo(callId, Clean(request), hookFields: null, response, _options.ArchiveDir);

    /// <summary><see cref="InsertCallIo"/> for a hook: the stdin's session fields go to
    /// <c>hook_sessions</c> once per value set and the request keeps only the event's fields
    /// (rtok T433). Raw saving, a body over the inline cap or one that does not split is saved
    /// whole. The hook path never archives.</summary>
    public void InsertHookCallIo(long callId, byte[] stdin, byte[]? response)
    {
        ArgumentNullException.ThrowIfNull(stdin);
        var split =
            _options.StoreRaw || stdin.Length > _options.CallIoInlineBytes
                ? null
                : HookFields.Split(stdin);
        if (split is var (body, fields))
        {
            WriteCallIo(callId, body, fields, response, archiveDir: null);
        }
        else
        {
            WriteCallIo(callId, Clean(stdin), hookFields: null, response, archiveDir: null);
        }
    }

    private byte[]? Clean(byte[]? request) =>
        request is null || _options.StoreRaw ? request : Sanitize.Body(request);

    /// <summary>The archive files are written before the transaction (SQLite cannot hold them);
    /// their <c>archive</c> rows and the <c>call_io</c> row commit together. A failed insert
    /// removes only the files this call created and no row references (rtok T208).</summary>
    private void WriteCallIo(
        long callId,
        byte[]? request,
        string? hookFields,
        byte[]? response,
        string? archiveDir
    )
    {
        var req = Spill(request, archiveDir);
        var res = Spill(response, archiveDir);
        try
        {
            using var scope = EnterWrite();
            var db = scope.Db;
            if (req.Archive is not null || res.Archive is not null)
            {
                var id = callId;
                var session = db.Calls.Where(c => c.Id == id).Select(c => c.SessionId).First();
                AddArchive(db, req, session);
                if (!string.Equals(res.Archive, req.Archive, StringComparison.Ordinal))
                {
                    AddArchive(db, res, session);
                }
            }
            db.CallIo.Add(
                new CallIo
                {
                    CallId = callId,
                    RequestBytes = req.Bytes,
                    ResponseBytes = res.Bytes,
                    RequestSha256 = req.Sha,
                    ResponseSha256 = res.Sha,
                    RequestJson = req.Text,
                    ResponseJson = res.Text,
                    RequestArchive = req.Archive,
                    ResponseArchive = res.Archive,
                    RequestRaw = req.Raw,
                    ResponseRaw = res.Raw,
                    HookSessionId = hookFields is null ? null : HookSessionId(db, hookFields),
                }
            );
            db.SaveChanges();
            scope.Commit();
        }
        catch
        {
            RemoveUnreferenced(req);
            RemoveUnreferenced(res);
            throw;
        }
    }

    /// <summary>A body staged for <c>call_io</c>. <see cref="Created"/> is true only when this
    /// call's write made the archive file.</summary>
    private readonly record struct Spilled(
        string? Text,
        string? Archive,
        long Bytes,
        string? Sha,
        string? Path,
        bool Created,
        byte[]? Raw
    );

    /// <summary>Within the cap: inline text, plus the exact bytes only when they are not valid
    /// UTF-8 (rtok T211). Over it: written to <paramref name="archiveDir"/> now, its row left to
    /// the caller's transaction; without a directory only the size is kept and the hash skipped
    /// (rtok T201).</summary>
    private Spilled Spill(byte[]? body, string? archiveDir)
    {
        if (body is null)
        {
            return default;
        }
        if (body.Length <= _options.CallIoInlineBytes)
        {
            var sha = StoreFiles.Sha256Hex(body);
            try
            {
                return new(
                    Sanitize.StrictUtf8.GetString(body),
                    null,
                    body.Length,
                    sha,
                    null,
                    false,
                    null
                );
            }
            catch (DecoderFallbackException)
            {
                return new(
                    Encoding.UTF8.GetString(body),
                    null,
                    body.Length,
                    sha,
                    null,
                    false,
                    body
                );
            }
        }
        if (archiveDir is null)
        {
            return new(null, null, body.Length, null, null, false, null);
        }
        var id = StoreFiles.Sha256Hex(body);
        var (path, created) = StoreFiles.WriteArchive(archiveDir, id, body);
        return new(null, id, body.Length, id, path, created, null);
    }

    /// <summary>The <c>archive</c> row behind a spilled body; the same body twice is one row.</summary>
    private void AddArchive(RtokDbContext context, Spilled body, string session)
    {
        if (body is not { Archive: { } id, Path: { } path })
        {
            return;
        }
        var db = context;
        var archiveId = id;
        if (db.Archive.Any(a => a.Id == archiveId))
        {
            return;
        }
        db.Archive.Add(
            new ArchiveEntry
            {
                Id = id,
                Ts = Now,
                Session = session,
                Bytes = body.Bytes,
                Path = path,
                Sha256 = id,
            }
        );
    }

    /// <summary>The <c>hook_sessions</c> id for <paramref name="fields"/>, inserted on first sight
    /// inside the caller's transaction.</summary>
    private static long HookSessionId(RtokDbContext context, string fields)
    {
        var db = context;
        var text = fields;
        if (
            db
                .HookSessions.Where(h => h.Fields == text)
                .Select(h => (long?)h.Id)
                .FirstOrDefault() is
            { } id
        )
        {
            return id;
        }
        var row = new HookSession { Fields = fields };
        db.HookSessions.Add(row);
        db.SaveChanges();
        return row.Id;
    }

    /// <summary>Removes an archive file this call created unless a row now references it: another
    /// writer may have committed the same body meanwhile. When that cannot be read, it stays.</summary>
    private void RemoveUnreferenced(Spilled body)
    {
        if (body is { Created: true, Archive: { } id, Path: { } path })
        {
            RemoveUnreferenced(id, path);
        }
    }

    /// <summary>Removes the archive file at <paramref name="path"/> unless a row references
    /// <paramref name="id"/>. When that cannot be read, it stays.</summary>
    private void RemoveUnreferenced(string id, string path)
    {
        try
        {
            using var scope = Enter();
            var db = scope.Db;
            var archiveId = id;
            if (!db.Archive.Any(a => a.Id == archiveId))
            {
                File.Delete(path);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Kept: a stray file is harmless, a deleted referenced one is lost data.
        }
    }
}
