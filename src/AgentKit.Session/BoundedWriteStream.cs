// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Collects serialized bytes into fixed-capacity storage and rejects only writes whose actual bytes exceed that capacity.</summary>
internal sealed class BoundedWriteStream: Stream
{
    private readonly byte[] _buffer;
    private int _written;

    /// <summary>Creates an empty stream with exactly the permitted payload capacity.</summary>
    /// <param name="capacity">The positive maximum number of bytes accepted across all writes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    internal BoundedWriteStream(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new byte[capacity];
    }

    /// <summary>Gets the exact initialized bytes accepted so far.</summary>
    /// <value>A read-only span over the fixed backing storage.</value>
    internal ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => _written;

    /// <inheritdoc/>
    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException("The bounded serialization stream does not support seeking.");
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("The bounded serialization stream is write-only.");

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("The bounded serialization stream does not support seeking.");

    /// <inheritdoc/>
    public override void SetLength(long value) =>
        throw new NotSupportedException("The bounded serialization stream has fixed capacity.");

    /// <summary>Writes the requested actual bytes when they fit within the remaining payload capacity.</summary>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="offset">The zero-based source offset.</param>
    /// <param name="count">The nonnegative number of bytes to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="count"/> is outside the source buffer.</exception>
    /// <exception cref="InvalidOperationException">The actual bytes would exceed the fixed payload capacity.</exception>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);
        Write(buffer.AsSpan(offset, count));
    }

    /// <summary>Writes the requested actual bytes when they fit within the remaining payload capacity.</summary>
    /// <param name="buffer">The bytes to append.</param>
    /// <exception cref="InvalidOperationException">The actual bytes would exceed the fixed payload capacity.</exception>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length > _buffer.Length - _written)
        {
            throw new InvalidOperationException("The codec payload exceeds its configured byte limit.");
        }

        buffer.CopyTo(_buffer.AsSpan(_written));
        _written += buffer.Length;
    }
}
