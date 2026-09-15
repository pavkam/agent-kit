// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Creates process-unique approval request identities.</summary>
internal sealed class GuidApprovalRequestIdGenerator: IIdentifierGenerator<ApprovalRequestId>
{
    /// <inheritdoc/>
    public ApprovalRequestId Create() => new(Guid.NewGuid());
}
