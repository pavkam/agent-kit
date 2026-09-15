// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents an authenticated terminal response bound to one approval request.</summary>
public sealed record ApprovalResponse
{
    /// <summary>Initializes a terminal approval response.</summary>
    /// <param name="id">The response identity used for idempotency.</param>
    /// <param name="requestId">The resolved request.</param>
    /// <param name="binding">The exact approved or denied scope.</param>
    /// <param name="resolution">The terminal decision.</param>
    /// <param name="approverIdentity">The complete identity authenticated by trusted broker ingress.</param>
    /// <param name="respondedAt">The response instant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> or <paramref name="approverIdentity"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default or <paramref name="resolution"/> is undefined.</exception>
    public ApprovalResponse(ApprovalResponseId id, ApprovalRequestId requestId, ApprovalScopeBinding binding,
        ApprovalResolution resolution, ExecutionIdentity approverIdentity, DateTimeOffset respondedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(requestId.Value, Guid.Empty);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentOutOfRangeException.ThrowIfUndefined(resolution);
        ArgumentNullException.ThrowIfNull(approverIdentity);
        Id = id;
        RequestId = requestId;
        Binding = binding;
        Resolution = resolution;
        ApproverIdentity = approverIdentity;
        RespondedAt = respondedAt;
    }

    /// <summary>Gets the idempotent response identity.</summary>
    public ApprovalResponseId Id { get; }
    /// <summary>Gets the resolved request identity.</summary>
    public ApprovalRequestId RequestId { get; }
    /// <summary>Gets the exact response binding.</summary>
    public ApprovalScopeBinding Binding { get; }
    /// <summary>Gets the terminal resolution.</summary>
    public ApprovalResolution Resolution { get; }
    /// <summary>Gets the complete authenticated approver identity.</summary>
    public ExecutionIdentity ApproverIdentity { get; }
    /// <summary>Gets the response instant.</summary>
    public DateTimeOffset RespondedAt { get; }
}
