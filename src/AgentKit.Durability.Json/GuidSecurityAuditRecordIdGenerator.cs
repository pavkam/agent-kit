// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json;

/// <summary>Creates collision-resistant audit-record identities for the JSON journal's protected ingress.</summary>
/// <remarks>
/// The identity only has to correlate one audit record with the grant consumption it describes, so a random GUID is
/// sufficient. A host that requires deterministic identities in tests replaces this registration.
/// </remarks>
internal sealed class GuidSecurityAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
{
    /// <inheritdoc/>
    public SecurityAuditRecordId Create() => new(Guid.NewGuid());
}
