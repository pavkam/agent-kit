// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Creates process-unique security audit record identities.</summary>
internal sealed class GuidSecurityAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
{
    /// <inheritdoc/>
    public SecurityAuditRecordId Create() => new(Guid.NewGuid());
}
