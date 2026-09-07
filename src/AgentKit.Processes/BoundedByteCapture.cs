// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Retains a complete byte stream only while it remains within a fixed preservation ceiling.</summary>
internal sealed class BoundedByteCapture
{
    private readonly int _capacity;
    private ArrayBufferWriter<byte>? _buffer;

    /// <summary>Initializes a positive complete-stream capture ceiling.</summary>
    /// <param name="capacity">The maximum complete byte count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    internal BoundedByteCapture(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _buffer = new ArrayBufferWriter<byte>(Math.Min(capacity, 81920));
    }

    /// <summary>Gets whether every observed byte remains available.</summary>
    internal bool IsComplete => _buffer is not null;

    /// <summary>Appends observed bytes or irreversibly abandons capture when the ceiling is exceeded.</summary>
    /// <param name="bytes">The next ordered bytes.</param>
    internal void Append(ReadOnlySpan<byte> bytes)
    {
        if (_buffer is null || bytes.IsEmpty)
        {
            return;
        }

        if (_buffer.WrittenCount > _capacity - bytes.Length)
        {
            _buffer = null;
            return;
        }

        _buffer.Write(bytes);
    }

    /// <summary>Returns the complete captured bytes.</summary>
    /// <returns>The complete immutable byte sequence.</returns>
    /// <exception cref="InvalidOperationException">The stream exceeded the capture ceiling.</exception>
    internal ImmutableArray<byte> ToImmutableArray()
    {
        return _buffer is null
            ? throw new InvalidOperationException("The complete byte stream exceeded its preservation ceiling.")
            : [.. _buffer.WrittenSpan];
    }
}
