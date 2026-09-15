// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// A read-only <see cref="Stream"/> test double that serves a fixed byte
/// payload in small, fixed-size fragments regardless of the buffer size a
/// caller requests, so streaming parsers can be proven correct at
/// arbitrary, adversarial fragmentation boundaries rather than only against
/// one convenient chunking.
/// </summary>
public sealed class ChunkedStream: Stream
{
    private readonly byte[] _payload;
    private readonly int _chunkSize;
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="ChunkedStream"/> class.</summary>
    /// <param name="payload">The complete byte payload to serve.</param>
    /// <param name="chunkSize">The maximum number of bytes returned by any single read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is less than one.</exception>
    public ChunkedStream(byte[] payload, int chunkSize)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        _payload = payload;
        _chunkSize = chunkSize;
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
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        var remaining = _payload.Length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        var toCopy = Math.Min(Math.Min(buffer.Length, _chunkSize), remaining);
        _payload.AsSpan(_position, toCopy).CopyTo(buffer);
        _position += toCopy;
        return toCopy;
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Task.FromResult(Read(buffer, offset, count));

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Read(buffer.Span));

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
