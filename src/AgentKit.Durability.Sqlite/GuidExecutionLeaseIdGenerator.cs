// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Creates collision-resistant lease identities for the SQLite adapter.</summary>
/// <remarks>
/// Lease identity only has to be unique; ordering and exclusion come from the database-allocated fencing token, so a
/// random GUID is sufficient and needs no coordination. A host that requires deterministic identities in tests replaces
/// this registration.
/// </remarks>
internal sealed class GuidExecutionLeaseIdGenerator: IIdentifierGenerator<ExecutionLeaseId>
{
    /// <inheritdoc/>
    public ExecutionLeaseId Create() => new(Guid.NewGuid());
}
