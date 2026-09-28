// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Creates collision-resistant audit-record identities for the in-memory durable journal.</summary>
/// <remarks>
/// This is a documented replaceable default registered by <c>AddInMemoryDurableOperationJournal</c>. A composition
/// that needs reproducible audit identities registers a deterministic
/// <see cref="IIdentifierGenerator{TIdentifier}"/> for <see cref="SecurityAuditRecordId"/> instead; the journal's
/// registration preserves an existing one.
/// </remarks>
internal sealed class GuidSecurityAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
{
    /// <inheritdoc/>
    public SecurityAuditRecordId Create() => new(Guid.NewGuid());
}
