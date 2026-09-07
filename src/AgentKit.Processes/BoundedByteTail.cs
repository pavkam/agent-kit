// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Retains the final bounded bytes of a stream while counting every observed byte.</summary>
internal sealed class BoundedByteTail
{
    private readonly byte[] _buffer;
    private int _start;
    private int _count;

    /// <summary>Initializes a positive-capacity byte tail.</summary>
    /// <param name="capacity">The positive retained byte capacity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    internal BoundedByteTail(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _buffer = new byte[capacity];
    }

    /// <summary>Gets the total bytes observed, including discarded prefix bytes.</summary>
    internal long TotalBytes { get; private set; }

    /// <summary>Gets whether any observed prefix bytes were discarded.</summary>
    internal bool IsTruncated => TotalBytes > _count;

    /// <summary>Appends bytes and retains only the newest bounded suffix.</summary>
    /// <param name="bytes">The bytes observed from the stream.</param>
    internal void Append(ReadOnlySpan<byte> bytes)
    {
        TotalBytes = checked(TotalBytes + bytes.Length);
        if (bytes.Length >= _buffer.Length)
        {
            bytes[^_buffer.Length..].CopyTo(_buffer);
            _start = 0;
            _count = _buffer.Length;
            return;
        }

        foreach (var value in bytes)
        {
            if (_count < _buffer.Length)
            {
                _buffer[(_start + _count) % _buffer.Length] = value;
                _count++;
            }
            else
            {
                _buffer[_start] = value;
                _start = (_start + 1) % _buffer.Length;
            }
        }
    }

    /// <summary>Copies the retained suffix into one immutable ordered value.</summary>
    /// <returns>The retained bytes in original stream order.</returns>
    internal ImmutableArray<byte> ToImmutableArray()
    {
        var result = ImmutableArray.CreateBuilder<byte>(_count);
        for (var index = 0; index < _count; index++)
        {
            result.Add(_buffer[(_start + index) % _buffer.Length]);
        }

        return result.MoveToImmutable();
    }
}
