// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Portable JSON mirror of <see cref="ApprovalRequest"/>, one durable request awaiting an authenticated human decision.</summary>
/// <remarks>
/// <para>
/// A pending request is the reason an operation is paused, so it is persisted in full and its identity is the store's
/// idempotency key. Recreating the same identity with different evidence is a conflict rather than an update, which is why
/// every member here is projected exactly and revalidated on read instead of being merged into existing state.
/// </para>
/// <para>
/// <see cref="SafePresentation"/> is the bounded text that was already redacted for an approver by the component that
/// created the request. It is persisted because it is part of what the approver actually saw, and it must never be treated
/// as loggable or exportable content by this adapter.
/// </para>
/// </remarks>
/// <param name="Id">The raw value of the non-empty stable request identity.</param>
/// <param name="Binding">The non-null exact authorization and grant constraints being asked about.</param>
/// <param name="SafePresentation">The non-blank bounded redacted approver text.</param>
/// <param name="CreatedAt">The instant the request was created.</param>
public sealed record JsonApprovalRequest(
    Guid Id,
    JsonApprovalScopeBinding Binding,
    string SafePresentation,
    DateTimeOffset CreatedAt)
{
    /// <summary>Projects one domain approval request into its portable JSON representation.</summary>
    /// <param name="value">The non-null request to project.</param>
    /// <returns>A document carrying the unwrapped identity and the complete projected binding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonApprovalRequest FromDomain(ApprovalRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonApprovalRequest(
            value.Id.Value,
            JsonApprovalScopeBinding.FromDomain(value.Binding),
            value.SafePresentation,
            value.CreatedAt);
    }

    /// <summary>Reconstructs the exact domain request this document was projected from.</summary>
    /// <returns>A request equal to the projected original, including its complete binding and ordered resources.</returns>
    /// <remarks>Reconstruction routes through the domain constructor, so an empty identity or blank presentation text in a tampered log fails closed instead of producing an unapprovable request.</remarks>
    /// <exception cref="ArgumentNullException"><see cref="Binding"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException"><see cref="SafePresentation"/> is blank, or the persisted binding is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Id"/> is empty, or the persisted binding violates its own bounds.</exception>
    public ApprovalRequest ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Binding);
        return new ApprovalRequest(
            new ApprovalRequestId(Id),
            Binding.ToDomain(),
            SafePresentation,
            CreatedAt);
    }
}
