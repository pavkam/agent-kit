// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for bounded process output events.</summary>
public abstract record ProcessOutputEvent
{
    /// <summary>Initializes one output event.</summary>
    /// <param name="sequence">The monotonic per-process sequence number.</param>
    private protected ProcessOutputEvent(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Sequence = sequence;
    }

    /// <summary>Gets the monotonic per-process sequence number.</summary>
    public long Sequence { get; init; }
}

/// <summary>Standard-output bytes observed from the child.</summary>
public sealed record ProcessStandardOutputBytes: ProcessOutputEvent
{
    /// <summary>Initializes one stdout event.</summary>
    /// <param name="sequence">The monotonic sequence number.</param>
    /// <param name="bytes">The observed bytes.</param>
    public ProcessStandardOutputBytes(long sequence, ReadOnlyMemory<byte> bytes)
        : base(sequence) => Bytes = bytes;

    /// <summary>Gets the observed bytes.</summary>
    public ReadOnlyMemory<byte> Bytes { get; init; }
}

/// <summary>Standard-error bytes observed from the child.</summary>
public sealed record ProcessStandardErrorBytes: ProcessOutputEvent
{
    /// <summary>Initializes one stderr event.</summary>
    /// <param name="sequence">The monotonic sequence number.</param>
    /// <param name="bytes">The observed bytes.</param>
    public ProcessStandardErrorBytes(long sequence, ReadOnlyMemory<byte> bytes)
        : base(sequence) => Bytes = bytes;

    /// <summary>Gets the observed bytes.</summary>
    public ReadOnlyMemory<byte> Bytes { get; init; }
}
