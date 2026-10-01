// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

/// <summary>Creates unique enforcement-intent identities in creation order for deterministic tests.</summary>
internal sealed class SequentialIntentIds: IIdentifierGenerator<SecurityEnforcementIntentId>
{
    private static long s_next;

    /// <inheritdoc/>
    public SecurityEnforcementIntentId Create()
    {
        var value = Interlocked.Increment(ref s_next);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 0x49;
        return new SecurityEnforcementIntentId(new Guid(bytes));
    }
}
