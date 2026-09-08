// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one immutable, redacted security transition for restricted audit delivery.</summary>
/// <remarks>Fields contain only pre-redacted fingerprints or closed safe structural facts. The record is append-oriented evidence and neither grants authority nor proves an external effect completed.</remarks>
public sealed record SecurityAuditRecord
{
    /// <summary>Initializes a redacted security audit record.</summary>
    /// <param name="id">The stable audit-record identity.</param>
    /// <param name="scope">The exact authorization scope that produced the transition.</param>
    /// <param name="requestId">The originating security-request identity.</param>
    /// <param name="grantId">The consumed or lifecycle grant identity when applicable.</param>
    /// <param name="approvalRequestId">The approval identity when applicable.</param>
    /// <param name="eventKind">The bounded audited transition kind.</param>
    /// <param name="outcome">The safe terminal transition outcome.</param>
    /// <param name="policyVersion">The effective policy version.</param>
    /// <param name="fields">The immutable map of named redacted fingerprints.</param>
    /// <param name="occurredAt">The injected-clock instant at which the transition became known.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/>, <paramref name="fields"/>, or a field value is null.</exception>
    /// <exception cref="ArgumentException">A field key is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/>, <paramref name="requestId"/>, <paramref name="policyVersion"/>, or a present optional identity is default, or <paramref name="eventKind"/> or <paramref name="outcome"/> is undefined.</exception>
    public SecurityAuditRecord(
        SecurityAuditRecordId id,
        SecurityAuthorizationScope scope,
        SecurityRequestId requestId,
        GrantId? grantId,
        ApprovalRequestId? approvalRequestId,
        SecurityAuditEventKind eventKind,
        SecurityAuditOutcome outcome,
        SecurityPolicyVersion policyVersion,
        ImmutableDictionary<string, RedactedAuditValue> fields,
        DateTimeOffset occurredAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty, nameof(id));
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfEqual(requestId.Value, Guid.Empty, nameof(requestId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(policyVersion.Value, nameof(policyVersion));
        if (grantId is { } presentGrantId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(presentGrantId.Value, Guid.Empty, nameof(grantId));
        }

        if (approvalRequestId is { } presentApprovalRequestId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(presentApprovalRequestId.Value, Guid.Empty, nameof(approvalRequestId));
        }

        ArgumentOutOfRangeException.ThrowIfUndefined(eventKind);
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        ArgumentNullException.ThrowIfNull(fields);
        foreach (var field in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field.Key, nameof(fields));
            ArgumentNullException.ThrowIfNull(field.Value, nameof(fields));
        }

        Id = id;
        Scope = scope;
        RequestId = requestId;
        GrantId = grantId;
        ApprovalRequestId = approvalRequestId;
        EventKind = eventKind;
        Outcome = outcome;
        PolicyVersion = policyVersion;
        Fields = fields;
        OccurredAt = occurredAt;
    }

    /// <summary>Gets the stable audit-record identity.</summary>
    public SecurityAuditRecordId Id { get; }
    /// <summary>Gets the exact authorization scope.</summary>
    public SecurityAuthorizationScope Scope { get; }
    /// <summary>Gets the originating security-request identity.</summary>
    public SecurityRequestId RequestId { get; }
    /// <summary>Gets the related grant identity when the transition has one.</summary>
    public GrantId? GrantId { get; }
    /// <summary>Gets the related approval identity when the transition has one.</summary>
    public ApprovalRequestId? ApprovalRequestId { get; }
    /// <summary>Gets the bounded audited transition kind.</summary>
    public SecurityAuditEventKind EventKind { get; }
    /// <summary>Gets the safe terminal transition outcome.</summary>
    public SecurityAuditOutcome Outcome { get; }
    /// <summary>Gets the effective policy version.</summary>
    public SecurityPolicyVersion PolicyVersion { get; }
    /// <summary>Gets immutable named redacted values.</summary>
    public ImmutableDictionary<string, RedactedAuditValue> Fields { get; }
    /// <summary>Gets the time at which the transition became known.</summary>
    public DateTimeOffset OccurredAt { get; }
}
