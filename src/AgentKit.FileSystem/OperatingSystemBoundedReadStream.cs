// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Enforces authorized byte bounds and snapshot-length truncation on a host read stream.</summary>
internal sealed class OperatingSystemBoundedReadStream: Stream
{
    private readonly Stream _inner;
    private readonly long _snapshotLength;
    private readonly long _maxBytes;
    private long _bytesRead;

    /// <summary>Initializes a bounded read stream over an opened regular file.</summary>
    /// <param name="inner">The host file stream.</param>
    /// <param name="snapshotLength">The content length observed at open time.</param>
    /// <param name="maxBytes">The authorized maximum bytes.</param>
    internal OperatingSystemBoundedReadStream(Stream inner, long snapshotLength, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshotLength);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        _inner = inner;
        _snapshotLength = snapshotLength;
        _maxBytes = maxBytes;
    }

    /// <summary>Gets whether reading stopped because the file grew after the opening stat.</summary>
    internal bool TruncatedDueToGrowth { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => _inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush() => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty || RemainingAllowance() == 0)
        {
            return 0;
        }

        var allowed = (int) Math.Min(buffer.Length, RemainingAllowance());
        var read = _inner.Read(buffer[..allowed]);
        if (read <= 0)
        {
            return 0;
        }

        _bytesRead += read;
        if (_bytesRead > _snapshotLength)
        {
            TruncatedDueToGrowth = true;
            var excess = _bytesRead - _snapshotLength;
            read -= (int) excess;
            _bytesRead = _snapshotLength;
            if (read <= 0)
            {
                return 0;
            }
        }

        return read;
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty || RemainingAllowance() == 0)
        {
            return 0;
        }

        var allowed = (int) Math.Min(buffer.Length, RemainingAllowance());
        var read = await _inner.ReadAsync(buffer[..allowed], cancellationToken).ConfigureAwait(false);
        if (read <= 0)
        {
            return 0;
        }

        _bytesRead += read;
        if (_bytesRead > _snapshotLength)
        {
            TruncatedDueToGrowth = true;
            var excess = _bytesRead - _snapshotLength;
            read -= (int) excess;
            _bytesRead = _snapshotLength;
            if (read <= 0)
            {
                return 0;
            }
        }

        return read;
    }

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

    private long RemainingAllowance()
    {
        var bound = Math.Min(_maxBytes, _snapshotLength);
        return Math.Max(0, bound - _bytesRead);
    }
}
