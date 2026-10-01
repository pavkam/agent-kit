// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a memory store to durably create one record.</summary>
/// <remarks>
/// Creation is idempotent by <see cref="IdempotencyKey"/> within the authorized tenant: an equivalent replay returns the
/// original record, and the same key with a different record is refused. The record must be created in a non-terminal state
/// at version <c>1</c>; the store assigns its ordering sequence. The <see cref="Grant"/> is single-use and binds this exact
/// operation, and its authorized tenant, agent, and principal must own the record.
/// </remarks>
public sealed record MemoryWriteRequest
{
    /// <summary>Initializes a validated write request.</summary>
    /// <param name="record">The record to create.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The record's state is not creatable, its version is not <c>1</c>, the key is blank, or the grant lacks captured authorization.</exception>
    public MemoryWriteRequest(DurableMemoryRecord record, IdempotencyKey idempotencyKey, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNotEqual(MemoryLifecycleTransitions.IsCreatable(record.State), true, nameof(record));
        ArgumentException.ThrowIfNotEqual(record.Version, new VersionToken("1"), nameof(record));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Record = record;
        IdempotencyKey = idempotencyKey;
        Grant = grant;
    }

    /// <summary>Gets the record to create.</summary>
    public DurableMemoryRecord Record { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
