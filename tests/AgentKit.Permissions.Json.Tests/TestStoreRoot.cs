// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Owns one canonical on-disk JSON store root and the raw file access a persistence case needs.</summary>
/// <remarks>
/// <para>
/// The durable adapters commit authority evidence as ordinary files, so proving crash recovery, corruption rejection, and
/// compaction requires reading and damaging those files directly rather than only calling the public store surface. This
/// fixture therefore exposes the exact manifest, lock, and record-log paths the adapters use, plus the narrow byte-level
/// mutations a case needs to model a torn append or a hand-edited log.
/// </para>
/// <para>
/// The directory is created eagerly and removed on disposal. A case that still holds an initialized store must dispose the
/// store first, because the advisory lock file is deleted only when its handle closes.
/// </para>
/// </remarks>
internal sealed class TestStoreRoot: IDisposable
{
    /// <summary>Creates one unique canonical directory that an adapter may treat as an existing store root.</summary>
    /// <remarks>The directory exists when the constructor returns, so <see cref="JsonStoreOpenMode.OpenExisting"/> reaches manifest validation instead of failing on a missing root.</remarks>
    internal TestStoreRoot() => Path = TestTemporaryDirectory.Create();

    /// <summary>Gets the fully qualified canonical directory this fixture owns.</summary>
    /// <value>An existing directory whose ancestry contains no symbolic link or reparse point.</value>
    internal string Path { get; }

    /// <summary>Gets the manifest document path the JSON store layout resolves inside this root.</summary>
    /// <value>The fully qualified <c>store.json</c> path, which need not exist yet.</value>
    internal string ManifestPath => System.IO.Path.Combine(Path, "store.json");

    /// <summary>Resolves one path inside this root without creating anything on disk.</summary>
    /// <param name="name">The single path-free file or directory name to append.</param>
    /// <returns>The fully qualified combined path, which need not exist.</returns>
    internal string Combine(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>Resolves the newline-delimited record-log path for one named log inside this root.</summary>
    /// <param name="name">The stable extension-free log name, such as <c>grants</c> or <c>approvals</c>.</param>
    /// <returns>The fully qualified <c>.jsonl</c> path, which need not exist.</returns>
    internal string LogPath(string name) => System.IO.Path.Combine(Path, $"{name}.jsonl");

    /// <summary>Reads the acknowledged lines currently present in one record log.</summary>
    /// <param name="name">The stable extension-free log name.</param>
    /// <returns>The ordered log lines, or an empty array when the log does not exist yet.</returns>
    /// <remarks>An incomplete trailing segment is returned as a final line, because the reader cannot distinguish framing here; use <see cref="LogBytes"/> when byte-exact framing matters.</remarks>
    internal string[] ReadLog(string name) => File.Exists(LogPath(name)) ? File.ReadAllLines(LogPath(name)) : [];

    /// <summary>Counts the lines currently present in one record log.</summary>
    /// <param name="name">The stable extension-free log name.</param>
    /// <returns>The current line count, which is zero for a missing log.</returns>
    internal int LogLineCount(string name) => ReadLog(name).Length;

    /// <summary>Reads the exact bytes of one record log.</summary>
    /// <param name="name">The stable extension-free log name.</param>
    /// <returns>The complete file bytes, or an empty array when the log does not exist yet.</returns>
    internal byte[] LogBytes(string name) => File.Exists(LogPath(name)) ? File.ReadAllBytes(LogPath(name)) : [];

    /// <summary>Appends one complete newline-terminated line to a record log.</summary>
    /// <param name="name">The stable extension-free log name.</param>
    /// <param name="line">The exact line content to append without its terminating newline.</param>
    /// <remarks>The line is written as a complete acknowledged record, so replay must treat a malformed payload as corruption rather than as a torn append.</remarks>
    internal void AppendLogLine(string name, string line) =>
        File.AppendAllText(LogPath(name), line + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>Removes trailing bytes from a record log so its final record is left unterminated.</summary>
    /// <param name="name">The stable extension-free log name.</param>
    /// <param name="count">The positive number of trailing bytes to discard.</param>
    /// <returns>The resulting file length in bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive or exceeds the current length.</exception>
    /// <remarks>This models the only damage a crash can cause: a record whose terminating newline never reached disk.</remarks>
    internal long TruncateTrailingBytes(string name, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        using var stream = new FileStream(LogPath(name), FileMode.Open, FileAccess.Write, FileShare.None);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, stream.Length, nameof(count));
        var length = stream.Length - count;
        stream.SetLength(length);
        return length;
    }

    /// <summary>Writes a complete manifest document into this root under the canonical encoding contract.</summary>
    /// <param name="manifest">The exact manifest evidence to persist.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is null.</exception>
    /// <remarks>Cases use this to plant identity, kind, schema, and fingerprint evidence an adapter must reject.</remarks>
    internal void WriteManifest(JsonStoreManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        File.WriteAllBytes(
            ManifestPath,
            JsonStoreSerialization.Encode(manifest, JsonStoreSerialization.CreateCanonicalOptions(), 1_048_576));
    }

    /// <summary>Reads back the manifest this root currently carries.</summary>
    /// <returns>The decoded manifest.</returns>
    /// <exception cref="FileNotFoundException">The root has not been initialized yet.</exception>
    internal JsonStoreManifest ReadManifest() => JsonStoreSerialization.Decode<JsonStoreManifest>(
        File.ReadAllBytes(ManifestPath), JsonStoreSerialization.CreateCanonicalOptions());

    /// <summary>Removes the directory and everything a case wrote into it.</summary>
    /// <remarks>Disposal is safe when the directory was already removed, and ignores a directory a still-open store handle keeps busy.</remarks>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A retained store handle can keep the root busy; leaked temporary directories are harmless to the suite.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best effort and never masks the case's own assertion failure.
        }
    }
}
