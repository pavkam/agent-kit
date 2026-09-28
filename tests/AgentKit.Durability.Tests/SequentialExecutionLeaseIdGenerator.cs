// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Allocates deterministic lease identities so a failing test names the same lease every run.</summary>
internal sealed class SequentialExecutionLeaseIdGenerator: IIdentifierGenerator<ExecutionLeaseId>
{
    private long _next;

    /// <inheritdoc/>
    public ExecutionLeaseId Create()
    {
        var value = Interlocked.Increment(ref _next);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 3;
        return new ExecutionLeaseId(new Guid(bytes));
    }
}
