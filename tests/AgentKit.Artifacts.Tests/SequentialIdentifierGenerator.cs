// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Creates deterministic, unique, non-empty GUID-backed identities in creation order.</summary>
/// <typeparam name="T">The identity type.</typeparam>
internal sealed class SequentialIdentifierGenerator<T>(Func<Guid, T> create, byte marker): IIdentifierGenerator<T>
    where T : struct
{
    private int _sequence;

    /// <inheritdoc/>
    public T Create()
    {
        var sequence = Interlocked.Increment(ref _sequence);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, sequence);
        bytes[15] = marker;
        return create(new Guid(bytes));
    }
}
