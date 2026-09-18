// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Appends and replays newline-delimited JSON records that form one store's authoritative transition log.</summary>
/// <remarks>
/// <para>
/// Each record occupies exactly one line and is flushed to disk before the append is acknowledged, so an acknowledged record
/// survives process loss. Because a line is only acknowledged after its terminating newline reaches disk, an unterminated
/// trailing segment can only be an append that never completed. Replay reports that segment separately so the owning adapter
/// can discard it under its declared recovery policy instead of treating a crash as corruption.
/// </para>
/// <para>
/// A newline-terminated line that fails to decode is genuine corruption and is never discarded. The log makes no
/// cross-process or distributed ordering claim; the owning adapter serializes writers.
/// </para>
/// </remarks>
public sealed class JsonRecordLog
{
    private const byte _newLine = (byte) '\n';
    private readonly int _maximumRecordBytes;

    /// <summary>Binds the log to one fixed path without creating or probing it.</summary>
    /// <param name="path">The fully qualified newline-delimited record-log path.</param>
    /// <param name="maximumRecordBytes">The positive inclusive maximum encoded length of a single record line.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumRecordBytes"/> is not positive.</exception>
    public JsonRecordLog(string path, int maximumRecordBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecordBytes);
        Path = path;
        _maximumRecordBytes = maximumRecordBytes;
    }

    /// <summary>Gets the fixed log path.</summary>
    /// <value>The fully qualified path supplied at construction; it is sensitive bootstrap configuration and is never logged.</value>
    public string Path { get; }

    /// <summary>Gets whether the log file currently exists.</summary>
    /// <value><see langword="true"/> when the exact path resolves to an existing file.</value>
    public bool Exists => File.Exists(Path);

    /// <summary>Appends one complete record and flushes it to disk before returning.</summary>
    /// <param name="record">The compact single-line encoded record without its terminating newline.</param>
    /// <param name="cancellationToken">Cancels before any byte is written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="record"/> exceeds the configured record bound.</exception>
    /// <exception cref="InvalidDataException"><paramref name="record"/> contains an embedded newline and would break line framing.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write begins.</exception>
    /// <exception cref="IOException">The record cannot be written or flushed.</exception>
    /// <remarks>Once the write begins it runs to completion; cancellation is not observed between the write and its flush.</remarks>
    public void Append(byte[] record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(record.Length, _maximumRecordBytes, nameof(record));
        if (Array.IndexOf(record, _newLine) >= 0)
        {
            throw new InvalidDataException("An encoded JSON store record contains a newline and cannot be framed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(record);
        stream.WriteByte(_newLine);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>Replays every acknowledged record in append order.</summary>
    /// <param name="cancellationToken">Cancels between decoded lines.</param>
    /// <returns>The ordered acknowledged record payloads and whether an incomplete trailing segment was observed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled during replay.</exception>
    /// <exception cref="InvalidDataException">A newline-terminated record exceeds its configured bound.</exception>
    /// <exception cref="IOException">The log cannot be read.</exception>
    /// <remarks>A missing log replays as an empty sequence, which is the correct state for a newly initialized store.</remarks>
    public JsonRecordLogReplay Replay(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(Path))
        {
            return new JsonRecordLogReplay([], false);
        }

        var content = File.ReadAllBytes(Path);
        var records = new List<ReadOnlyMemory<byte>>();
        var start = 0;
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] != _newLine)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var length = index - start;
            if (length > _maximumRecordBytes)
            {
                throw new InvalidDataException("A persisted JSON store record exceeds its configured byte bound.");
            }
            if (length > 0)
            {
                records.Add(content.AsMemory(start, length));
            }

            start = index + 1;
        }

        return new JsonRecordLogReplay(records, start < content.Length);
    }

    /// <summary>Atomically replaces the whole log with a compacted ordered record set.</summary>
    /// <param name="records">The ordered compact record payloads that fully describe current state.</param>
    /// <param name="cancellationToken">Cancels before the compacted file is renamed over the log.</param>
    /// <exception cref="ArgumentNullException"><paramref name="records"/> is null.</exception>
    /// <exception cref="InvalidDataException">A supplied record contains an embedded newline or exceeds its bound.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the rename.</exception>
    /// <exception cref="IOException">The compacted log cannot be written, flushed, or renamed.</exception>
    /// <remarks>
    /// Compaction discards an incomplete trailing segment by construction, because it writes a fresh file from the supplied
    /// authoritative state. A crash before the rename leaves the previous complete log untouched.
    /// </remarks>
    public void Compact(IReadOnlyList<byte[]> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);
        cancellationToken.ThrowIfCancellationRequested();
        var temporaryPath = $"{Path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                foreach (var record in records)
                {
                    if (record.Length > _maximumRecordBytes || Array.IndexOf(record, _newLine) >= 0)
                    {
                        throw new InvalidDataException("A compacted JSON store record is unframeable or exceeds its bound.");
                    }

                    stream.Write(record);
                    stream.WriteByte(_newLine);
                }

                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, Path, overwrite: true);
        }
        catch
        {
            JsonAtomicDocument.TryDelete(temporaryPath);
            throw;
        }
    }
}
