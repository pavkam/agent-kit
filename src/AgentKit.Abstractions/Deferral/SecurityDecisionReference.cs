// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>References the audited security decision and captured policy that preceded a deferral.</summary>
/// <remarks>This reference carries no grant. The resolution owner loads the exact audit evidence and checks ownership, scope, inputs, current authority and revocation; construction does not attest audit persistence.</remarks>
public sealed record SecurityDecisionReference
{
    /// <summary>Captures initialized correlation for a decision already recorded by its owner.</summary>
    /// <param name="requestId">The nondefault evaluated security request.</param>
    /// <param name="policySnapshot">The nonnull immutable effective-policy reference.</param>
    /// <param name="auditRecordId">The nondefault audit record retaining that exact decision.</param>
    /// <exception cref="ArgumentOutOfRangeException">A request or audit identity is default.</exception>
    /// <exception cref="ArgumentNullException">The policy snapshot is null.</exception>
    public SecurityDecisionReference(SecurityRequestId requestId, SecurityPolicySnapshotReference policySnapshot, SecurityAuditRecordId auditRecordId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(requestId, default);
        ArgumentNullException.ThrowIfNull(policySnapshot);
        ArgumentOutOfRangeException.ThrowIfEqual(auditRecordId, default);
        RequestId = requestId; PolicySnapshot = policySnapshot; AuditRecordId = auditRecordId;
    }
    /// <summary>Gets the evaluated request identity to resolve and revalidate.</summary>
    /// <value>A nondefault security request identity, not an authorization capability.</value>
    public SecurityRequestId RequestId { get; }
    /// <summary>Gets the captured policy provenance.</summary>
    /// <value>A nonnull snapshot reference subject to current revocation and scope checks.</value>
    public SecurityPolicySnapshotReference PolicySnapshot { get; }
    /// <summary>Gets the durable decision evidence reference.</summary>
    /// <value>A nondefault audit identity; the storage owner verifies that the referenced record exists and matches.</value>
    public SecurityAuditRecordId AuditRecordId { get; }
}
