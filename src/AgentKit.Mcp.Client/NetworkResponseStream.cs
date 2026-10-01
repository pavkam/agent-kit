// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Exposes one network response body as a read-only stream that owns and releases the response.</summary>
/// <remarks>
/// The wrapped body is already bounded while streaming by the network boundary; this stream adds no buffering and no
/// bound of its own. Disposal starts exactly one disposal of the owning <see cref="INetworkResponse"/>, whether it
/// arrives synchronously or asynchronously and however many times it is requested.
/// </remarks>
/// <param name="response">The owned response; it is disposed with this stream.</param>
internal sealed class NetworkResponseStream(INetworkResponse response): Stream
{
    private readonly Stream _body = response.Content;
    private int _disposed;

    /// <inheritdoc/>
    public override bool CanRead => _disposed == 0 && _body.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException("The response body is a forward-only stream.");

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException("The response body is a forward-only stream.");
        set => throw new NotSupportedException("The response body is a forward-only stream.");
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _body.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _body.Read(buffer);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _body.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _body.ReadAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("The response body is a forward-only stream.");

    /// <inheritdoc/>
    public override void SetLength(long value) =>
        throw new NotSupportedException("The response body is read-only.");

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The response body is read-only.");

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await response.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A synchronous caller cannot await, so a response whose asynchronous disposal has not completed inline keeps
    /// finishing in the background with its failure observed and dropped; disposal is still started exactly once.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            var disposal = response.DisposeAsync();
            if (!disposal.IsCompletedSuccessfully)
            {
                _ = ObserveAsync(disposal);
            }
        }

        base.Dispose(disposing);
    }

    private static async Task ObserveAsync(ValueTask disposal)
    {
        try
        {
            await disposal.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Disposal failure after the caller has finished with the stream has no observer left to inform.
        }
    }
}
