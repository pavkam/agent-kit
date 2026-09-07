// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Stops response consumption when actual bytes or the response deadline exceed fixed boundaries.</summary>
internal sealed class BoundedReadStream: Stream
{
    private readonly Stream _inner;
    private readonly long _maximumBytes;
    private readonly CancellationToken _timeoutToken;
    private long _observedBytes;

    /// <summary>Initializes one bounded view over an owned response stream.</summary>
    /// <param name="inner">The readable stream owned by the response handle.</param>
    /// <param name="maximumBytes">The positive maximum bytes that may be observed.</param>
    /// <param name="timeoutToken">The transport-owned response deadline token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    internal BoundedReadStream(Stream inner, long maximumBytes, CancellationToken timeoutToken)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _inner = inner;
        _maximumBytes = maximumBytes;
        _timeoutToken = timeoutToken;
    }

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
        get => _observedBytes;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfTimedOut();
        var read = _inner.Read(buffer, offset, count);
        ThrowIfTimedOut();
        Observe(read);
        return read;
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ThrowIfTimedOut();
        var read = _inner.Read(buffer);
        ThrowIfTimedOut();
        Observe(read);
        return read;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfTimedOut();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _timeoutToken);
        try
        {
            var read = await _inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
            Observe(read);
            return read;
        }
        catch (OperationCanceledException exception)
            when (_timeoutToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new NetworkResponseTimedOutException(exception);
        }
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadArrayAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc/>
    public override void Flush()
    {
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

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private Task<int> ReadArrayAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Observe(int read)
    {
        _observedBytes += read;
        if (_observedBytes > _maximumBytes)
        {
            throw new NetworkResponseTooLargeException(_maximumBytes, _observedBytes);
        }
    }

    private void ThrowIfTimedOut()
    {
        if (_timeoutToken.IsCancellationRequested)
        {
            throw new NetworkResponseTimedOutException();
        }
    }
}
