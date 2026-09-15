// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents a durable, exactly bound request for human approval.</summary>
public sealed record ApprovalRequest
{
    /// <summary>Initializes an approval request.</summary>
    /// <param name="id">The request identity.</param>
    /// <param name="binding">The exact authorization and grant constraints.</param>
    /// <param name="safePresentation">Bounded redacted text suitable for an approver.</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <exception cref="ArgumentNullException"><paramref name="binding"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="safePresentation"/> is blank.</exception>
    public ApprovalRequest(ApprovalRequestId id, ApprovalScopeBinding binding, string safePresentation,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id.Value, Guid.Empty);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(safePresentation);
        Id = id;
        Binding = binding;
        SafePresentation = safePresentation;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the stable request identity.</summary>
    public ApprovalRequestId Id { get; }
    /// <summary>Gets the exact authorization binding.</summary>
    public ApprovalScopeBinding Binding { get; }
    /// <summary>Gets bounded redacted approver text.</summary>
    public string SafePresentation { get; }
    /// <summary>Gets the creation instant.</summary>
    public DateTimeOffset CreatedAt { get; }
}
