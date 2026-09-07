// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>A read-only stream wrapper that enforces a maximum total byte count while reading, not after buffering.</summary>
/// <remarks>
/// Exceeding the configured maximum throws <see cref="NetworkResponseTooLargeException"/>
/// from the read call that would cross the bound; bytes already delivered
/// to the caller in prior reads are never retracted, and no further bytes
/// are read from the inner stream afterward.
/// </remarks>
internal sealed class BoundedReadStream: Stream
{
    private readonly Stream _inner;
    private readonly long _maximumBytes;
    private long _bytesRead;

    /// <summary>Initializes a new instance of the <see cref="BoundedReadStream"/> class.</summary>
    /// <param name="inner">The stream to read from.</param>
    /// <param name="maximumBytes">The maximum total number of bytes this stream allows reading.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is negative.</exception>
    public BoundedReadStream(Stream inner, long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);

        _inner = inner;
        _maximumBytes = maximumBytes;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _bytesRead += read;

        return _bytesRead > _maximumBytes
            ? throw new NetworkResponseTooLargeException(_bytesRead, _maximumBytes)
            : read;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
