// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Owns one canonical on-disk JSON journal root and the raw file access a persistence case needs.</summary>
/// <remarks>
/// <para>
/// The adapter commits recovery evidence as ordinary files, so proving crash recovery, corruption rejection, and
/// compaction requires reading and damaging those files directly rather than only calling the public journal surface.
/// This fixture exposes the exact manifest and record-log paths the adapter uses, plus the narrow byte-level mutations
/// a case needs to model a torn append or a hand-edited log.
/// </para>
/// <para>
/// The directory is created eagerly, so a case may open it with
/// <see cref="JsonStoreOpenMode.OpenExisting"/> and reach manifest validation instead of failing on a missing root.
/// The macOS temporary root resolves through a symbolic link, which the store rejects, so the prefix is canonicalized
/// to keep cases exercising the real validation.
/// </para>
/// </remarks>
internal sealed class JsonDurableTestRoot
{
    private const string _logName = "operations";

    /// <summary>Creates one unique canonical directory an adapter may treat as an existing journal root.</summary>
    internal JsonDurableTestRoot()
    {
        var root = Path.GetTempPath();
        if (OperatingSystem.IsMacOS() && root.StartsWith("/var/", StringComparison.Ordinal))
        {
            root = "/private" + root;
        }

        DirectoryPath = Path.Combine(root, $"agentkit-durability-json-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(DirectoryPath);
        StoreInstanceId = new JsonDurableStoreInstanceId(Guid.NewGuid());
    }

    /// <summary>Gets the fully qualified canonical directory this fixture owns.</summary>
    internal string DirectoryPath { get; }

    /// <summary>Gets the persistent identity every target over this root expects.</summary>
    internal JsonDurableStoreInstanceId StoreInstanceId { get; }

    /// <summary>Gets the manifest document path the JSON store layout resolves inside this root.</summary>
    internal string ManifestPath => Path.Combine(DirectoryPath, "store.json");

    /// <summary>Gets the newline-delimited record-log path the journal appends to.</summary>
    internal string LogPath => Path.Combine(DirectoryPath, $"{_logName}.jsonl");

    /// <summary>Builds the host-authorized target for this root.</summary>
    /// <param name="openMode">Whether the root and manifest may be created.</param>
    /// <param name="recoveryMode">Whether an incomplete trailing append may be discarded.</param>
    /// <returns>An immutable target naming this exact root.</returns>
    internal JsonDurableStoreTarget Target(
        JsonStoreOpenMode openMode = JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode recoveryMode = JsonStoreRecoveryMode.RecoverTornAppends) =>
        new(DirectoryPath, StoreInstanceId, openMode, recoveryMode);

    /// <summary>Reads the acknowledged lines currently present in the record log.</summary>
    /// <returns>The ordered log lines, or an empty array when the log does not exist yet.</returns>
    internal string[] ReadLog() => File.Exists(LogPath) ? File.ReadAllLines(LogPath) : [];

    /// <summary>Counts the lines currently present in the record log.</summary>
    /// <returns>The current line count, which is zero for a missing log.</returns>
    internal int LogLineCount() => ReadLog().Length;

    /// <summary>Appends one complete newline-terminated line to the record log.</summary>
    /// <param name="line">The exact line content to append without its terminating newline.</param>
    /// <remarks>The line is written as a complete acknowledged record, so replay must treat a malformed payload as corruption rather than as a torn append.</remarks>
    internal void AppendLogLine(string line) =>
        File.AppendAllText(LogPath, line + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>Removes the record log's trailing newline so its final record is left unterminated.</summary>
    /// <exception cref="InvalidOperationException">The log is empty, so no complete record could be torn.</exception>
    /// <remarks>This models the only damage a crash can cause: a record whose terminating newline never reached disk.</remarks>
    internal void TearTrailingRecord()
    {
        using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Write, FileShare.None);
        if (stream.Length == 0)
        {
            throw new InvalidOperationException("The record log has no complete record to tear.");
        }

        stream.SetLength(stream.Length - 1);
    }

    /// <summary>Writes a complete manifest document into this root under the canonical encoding contract.</summary>
    /// <param name="manifest">The exact manifest evidence to persist.</param>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is null.</exception>
    /// <remarks>Cases use this to plant identity, kind, schema, and fingerprint evidence the adapter must reject.</remarks>
    internal void WriteManifest(JsonStoreManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        File.WriteAllBytes(
            ManifestPath,
            JsonStoreSerialization.Encode(manifest, JsonStoreSerialization.CreateCanonicalOptions(), 1_048_576));
    }
}
