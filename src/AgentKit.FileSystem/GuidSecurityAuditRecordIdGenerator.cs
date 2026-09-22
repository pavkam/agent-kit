// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Creates random <see cref="SecurityAuditRecordId"/> values for host audit records.</summary>
internal sealed class GuidSecurityAuditRecordIdGenerator: IIdentifierGenerator<SecurityAuditRecordId>
{
    /// <inheritdoc/>
    public SecurityAuditRecordId Create() => new(Guid.NewGuid());
}
