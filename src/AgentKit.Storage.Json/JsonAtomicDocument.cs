// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Reads and replaces one whole JSON document so a reader never observes a partially written file.</summary>
/// <remarks>
/// Replacement writes a sibling temporary file in the same directory, flushes it to disk, and then renames it over the
/// target. Rename within one directory is atomic on every supported host filesystem, so a crash leaves either the previous
/// complete document or the new complete document. Durability of the rename itself is delegated to the operating system;
/// this adapter does not claim distributed or cross-directory atomicity.
/// </remarks>
public static class JsonAtomicDocument
{
    /// <summary>Reads one complete document, enforcing a byte bound before any decoding work.</summary>
    /// <param name="path">The fully qualified document path.</param>
    /// <param name="maximumBytes">The positive inclusive maximum accepted document length.</param>
    /// <returns>The exact file bytes, or <see langword="null"/> when the document does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    /// <exception cref="InvalidDataException">The existing document exceeds <paramref name="maximumBytes"/>.</exception>
    /// <exception cref="IOException">The document cannot be read.</exception>
    public static byte[]? Read(string path, int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        using var stream = TryOpenRead(path);
        if (stream is null)
        {
            return null;
        }
        if (stream.Length > maximumBytes)
        {
            throw new InvalidDataException("The JSON store document exceeds its configured byte bound.");
        }

        var payload = new byte[checked((int) stream.Length)];
        stream.ReadExactly(payload);
        return payload;
    }

    /// <summary>Replaces one document atomically with the supplied complete payload.</summary>
    /// <param name="path">The fully qualified target document path.</param>
    /// <param name="payload">The complete encoded document bytes.</param>
    /// <param name="cancellationToken">Cancels before the temporary file is renamed over the target.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the rename.</exception>
    /// <exception cref="IOException">The temporary file cannot be written, flushed, or renamed.</exception>
    /// <remarks>
    /// Cancellation after the rename begins is not observed, so a cancelled call either left the previous document intact or
    /// completed the replacement. The temporary file is removed on every failure path.
    /// </remarks>
    public static void Replace(string path, byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    /// <summary>Removes a leftover temporary file without masking an in-flight failure.</summary>
    /// <param name="path">The temporary path to remove.</param>
    /// <remarks>Cleanup failures are intentionally ignored so the original error reaches the caller unchanged.</remarks>
    public static void TryDelete(string path)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(path), "A temporary path is required.");
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup never replaces the originating failure.
        }
    }

    private static FileStream? TryOpenRead(string path)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(path), "A validated document path is required.");
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
