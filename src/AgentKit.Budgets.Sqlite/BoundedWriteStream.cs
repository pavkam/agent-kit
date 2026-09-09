// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Rejects an encoded payload before a write can grow its backing buffer beyond the configured bound.</summary>
internal sealed class BoundedWriteStream: MemoryStream
{
    private readonly int _maximumLength;

    /// <summary>Creates an empty writable stream with a positive maximum length.</summary>
    /// <param name="maximumLength">The positive maximum encoded byte count.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumLength"/> is not positive.</exception>
    internal BoundedWriteStream(int maximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        _maximumLength = maximumLength;
    }

    /// <inheritdoc/>
    public override int Capacity
    {
        get => base.Capacity;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, _maximumLength);
            base.Capacity = value;
        }
    }

    /// <summary>Validates that a complete encoded field can fit before its temporary storage is allocated.</summary>
    /// <param name="byteCount">The nonnegative field size, including its framing bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="byteCount"/> is negative or exceeds the remaining envelope capacity.</exception>
    internal void EnsureCanWrite(int byteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(byteCount, _maximumLength - Position, nameof(byteCount));
        EnsureCapacity(byteCount);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, buffer.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length - offset);
        EnsureCapacity(count);
        base.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureCapacity(buffer.Length);
        base.Write(buffer);
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value)
    {
        EnsureCapacity(1);
        base.WriteByte(value);
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, _maximumLength);
        if (value > Capacity)
        {
            Capacity = checked((int) value);
        }
        base.SetLength(value);
    }

    private void EnsureCapacity(int additionalLength)
    {
        Debug.Assert(additionalLength >= 0, "Stream writers supply a nonnegative byte count.");
        if (Position > _maximumLength - additionalLength)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalLength), "The encoded budget evidence exceeds its configured bound.");
        }
        var required = checked((int) Position + additionalLength);
        if (required > Capacity)
        {
            var doubled = Capacity == 0 ? Math.Min(256, _maximumLength) : Math.Min((long) Capacity * 2, _maximumLength);
            Capacity = checked((int) Math.Max(required, doubled));
        }
    }
}
