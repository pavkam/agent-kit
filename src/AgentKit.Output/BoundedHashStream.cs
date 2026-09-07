// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

using System.Security.Cryptography;

/// <summary>Hashes deterministic schema serialization while rejecting writes beyond a captured byte limit.</summary>
internal sealed class BoundedHashStream: Stream
{
    private readonly CancellationToken _cancellationToken;
    private readonly IncrementalHash _hash;
    private readonly int _maximumBytes;
    private int _bytesWritten;

    /// <summary>Initializes a bounded hashing sink without allocating hash state before validating its byte limit.</summary>
    /// <param name="maximumBytes">The positive maximum number of accepted bytes.</param>
    /// <param name="cancellationToken">The token checked before every write.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    public BoundedHashStream(int maximumBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _maximumBytes = maximumBytes;
        _cancellationToken = cancellationToken;
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    }

    /// <summary>Finishes the current hash after all serialized bytes have been written and resets the hash state.</summary>
    /// <returns>The lowercase SHA-256 content identity.</returns>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    /// <remarks>Bytes written after this call contribute to a new hash while the cumulative byte limit remains unchanged.</remarks>
    public ContentHash CompleteHash() =>
        new("sha256:" + Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant());

    /// <summary>Hashes one span after enforcing cancellation and the cumulative byte limit.</summary>
    /// <param name="buffer">The bytes to hash without retaining their content.</param>
    /// <exception cref="OperationCanceledException">The captured cancellation token has been cancelled.</exception>
    /// <exception cref="OutputSchemaSizeLimitException">The write would exceed the captured cumulative byte limit.</exception>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (buffer.Length > _maximumBytes - _bytesWritten)
        {
            throw new OutputSchemaSizeLimitException();
        }

        _hash.AppendData(buffer);
        _bytesWritten += buffer.Length;
    }

    /// <summary>Hashes one array segment after enforcing the array bounds, cancellation, and cumulative byte limit.</summary>
    /// <param name="buffer">The source byte array.</param>
    /// <param name="offset">The zero-based starting offset.</param>
    /// <param name="count">The number of bytes to hash.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> lies outside <paramref name="buffer"/>.</exception>
    /// <exception cref="OperationCanceledException">The captured cancellation token has been cancelled.</exception>
    /// <exception cref="OutputSchemaSizeLimitException">The write would exceed the captured cumulative byte limit.</exception>
    /// <exception cref="ObjectDisposedException">The stream has been disposed.</exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);
        Write(buffer.AsSpan(offset, count));
    }

    /// <summary>Releases the incremental hash when disposing the stream; repeated disposal is harmless.</summary>
    /// <param name="disposing">Whether managed resources must be released.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Performs no buffered I/O after checking the captured cancellation token.</summary>
    /// <exception cref="OperationCanceledException">The captured cancellation token has been cancelled.</exception>
    public override bool CanRead => false;
    /// <inheritdoc/>
    public override bool CanSeek => false;
    /// <inheritdoc/>
    public override bool CanWrite => true;
    /// <inheritdoc/>
    public override long Length => _bytesWritten;
    /// <inheritdoc/>
    public override long Position { get => _bytesWritten; set => throw new NotSupportedException(); }
    /// <inheritdoc/>
    public override void Flush() => _cancellationToken.ThrowIfCancellationRequested();
    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
}
