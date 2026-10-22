using System.IO.Abstractions;
using System.Text;
using Rtok.Agent.Sdk;

namespace Rtok.Mcp.Install;

/// <summary>
/// The file-system seam the MCP core reads and writes host configs through (rtok D29). The CLI
/// passes the real file system and <see cref="HostFile.Write"/>; tests pass an in-memory one.
/// </summary>
/// <param name="fs">Where files are read.</param>
/// <param name="write">Where files are written; by default straight into <paramref name="fs"/>, gated by the run.</param>
/// <param name="delete">How files are deleted; by default straight from <paramref name="fs"/>, gated by the run.</param>
public sealed class McpDisk(IFileSystem fs, HostWrite? write = null, HostDelete? delete = null)
{
    private static readonly UTF8Encoding Strict = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    /// <summary>The file system files are read from.</summary>
    public IFileSystem Fs { get; } = fs ?? throw new ArgumentNullException(nameof(fs));

    /// <summary>
    /// The text of <paramref name="path"/>, or null when there is no such file. A file that exists
    /// but cannot be read, or is not UTF-8, throws: an unreadable config is refused, never taken
    /// for an absent one and replaced (rtok T652).
    /// </summary>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not UTF-8.</exception>
    public string? Read(string path)
    {
        if (!Fs.File.Exists(path))
        {
            return null;
        }
        byte[] bytes;
        try
        {
            bytes = Fs.File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"{path}: unreadable ({e.Message}); rtok leaves it as it is", e);
        }
        try
        {
            var text = Strict.GetString(bytes);
            return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
        }
        catch (DecoderFallbackException e)
        {
            throw new InvalidDataException($"{path}: not UTF-8; rtok leaves it as it is", e);
        }
    }

    /// <summary>Writes <paramref name="body"/> to <paramref name="path"/> when <paramref name="apply"/> lets <paramref name="report"/> write.</summary>
    public void Write(Apply apply, string path, string body, string report)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (write is not null)
        {
            write(apply, path, body, report);
            return;
        }
        if (!apply.Writes(report))
        {
            return;
        }
        if (Fs.Path.GetDirectoryName(path) is { Length: > 0 } dir)
        {
            Fs.Directory.CreateDirectory(dir);
        }
        Fs.File.WriteAllText(path, body);
    }

    /// <summary>Deletes <paramref name="path"/> when <paramref name="apply"/> lets <paramref name="report"/> write.</summary>
    public void Delete(Apply apply, string path, string report)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (delete is not null)
        {
            delete(apply, path, report);
            return;
        }
        if (apply.Writes(report) && Fs.File.Exists(path))
        {
            Fs.File.Delete(path);
        }
    }
}
