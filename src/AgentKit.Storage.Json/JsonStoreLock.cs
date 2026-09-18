// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json;

/// <summary>Holds one host-local advisory exclusive lock so a second writer fails fast instead of corrupting a store root.</summary>
/// <remarks>
/// <para>
/// The lock is a file opened for the adapter's lifetime with no sharing. A second process or a second adapter instance
/// targeting the same root cannot open it and therefore cannot interleave appends. This is honest single-writer protection
/// on one host; it is not a distributed lease, provides no fencing token, and proves nothing about an external effect.
/// </para>
/// <para>
/// A stale lock cannot outlive its owning process: the operating system releases the handle when the process exits, so
/// recovery after a crash does not require manual cleanup.
/// </para>
/// </remarks>
public sealed class JsonStoreLock: IDisposable
{
    private FileStream? _stream;

    private JsonStoreLock(FileStream stream)
    {
        Debug.Assert(stream is not null, "An acquired exclusive stream is required.");
        _stream = stream;
    }

    /// <summary>Acquires the exclusive advisory lock for one store root.</summary>
    /// <param name="path">The fully qualified lock-file path inside the validated root.</param>
    /// <returns>The held lock, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null or blank.</exception>
    /// <exception cref="InvalidOperationException">Another writer already holds the store root.</exception>
    /// <exception cref="IOException">The lock file cannot be created or opened for a reason other than contention.</exception>
    public static JsonStoreLock Acquire(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        JsonStoreRoot.ValidateFile(path);
        try
        {
            return new JsonStoreLock(new FileStream(
                path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 1,
                FileOptions.DeleteOnClose));
        }
        catch (IOException exception) when (exception is not FileNotFoundException and not DirectoryNotFoundException)
        {
            throw new InvalidOperationException(
                "The JSON store root is already held by another writer on this host.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidOperationException(
                "The JSON store root lock could not be acquired with the configured access.", exception);
        }
    }

    /// <summary>Releases the advisory lock and removes its file.</summary>
    /// <remarks>Disposal is idempotent. Releasing does not flush or validate store content; the owning adapter does that first.</remarks>
    public void Dispose()
    {
        var stream = Interlocked.Exchange(ref _stream, null);
        stream?.Dispose();
    }
}
