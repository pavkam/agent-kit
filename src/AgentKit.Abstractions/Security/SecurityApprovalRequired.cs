// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that policy neither allowed nor denied the request inline and created a durable approval request.</summary>
public sealed record SecurityApprovalRequired: SecurityDecision
{
    /// <summary>Initializes an approval-required decision.</summary>
    /// <param name="requestId">The evaluated request.</param>
    /// <param name="policyVersion">The effective policy version.</param>
    /// <param name="approval">The durable approval request created for this decision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="approval"/> is null.</exception>
    public SecurityApprovalRequired(SecurityRequestId requestId, SecurityPolicyVersion policyVersion, ApprovalRequest approval)
        : base(requestId, policyVersion)
    {
        ArgumentNullException.ThrowIfNull(approval);
        Approval = approval;
    }

    /// <summary>Gets the durable approval request.</summary>
    public ApprovalRequest Approval { get; init; }
}
