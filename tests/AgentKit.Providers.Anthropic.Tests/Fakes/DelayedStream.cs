// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Tests.Fakes;

/// <summary>
/// A read-only <see cref="Stream"/> test double that serves a fixed byte
/// payload in small fragments, each preceded by a real asynchronous delay
/// that honors cancellation, so a test can deterministically cancel an
/// in-progress read while a streaming parser is mid-flight.
/// </summary>
internal sealed class DelayedStream: Stream
{
    private const int MaxFragmentSize = 8;

    private readonly byte[] _payload;
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="DelayedStream"/> class.</summary>
    /// <param name="payload">The complete byte payload to serve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
    public DelayedStream(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payload = payload;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _payload.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);

        var remaining = _payload.Length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        var toCopy = Math.Min(Math.Min(buffer.Length, MaxFragmentSize), remaining);
        _payload.AsSpan(_position, toCopy).CopyTo(buffer.Span);
        _position += toCopy;
        return toCopy;
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

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
}
