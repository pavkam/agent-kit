// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Portable JSON mirror of <see cref="ApprovalResponse"/>, one authenticated terminal decision bound to a single request.</summary>
/// <remarks>
/// <para>
/// A response is terminal: once persisted it is never replaced, and a second decision under the same identity is a conflict.
/// The document therefore carries the complete approved or denied <see cref="Binding"/> rather than only the request
/// reference, so replay can prove the decision was made about the exact scope the request described.
/// </para>
/// <para>
/// <see cref="ApproverIdentity"/> is the full identity that trusted broker ingress authenticated. It is persisted as
/// evidence of who decided and never as authority in itself; the approver identity grants nothing, and the resulting grant
/// is still issued and bounded separately.
/// </para>
/// </remarks>
/// <param name="Id">The raw value of the non-empty idempotent response identity.</param>
/// <param name="RequestId">The raw value of the non-empty request this response resolves.</param>
/// <param name="Binding">The non-null exact scope that was approved or denied.</param>
/// <param name="Resolution">The terminal decision.</param>
/// <param name="ApproverIdentity">The non-null complete identity authenticated by the trusted approval ingress.</param>
/// <param name="RespondedAt">The instant the decision was recorded.</param>
public sealed record JsonApprovalResponse(
    Guid Id,
    Guid RequestId,
    JsonApprovalScopeBinding Binding,
    ApprovalResolution Resolution,
    JsonExecutionIdentity ApproverIdentity,
    DateTimeOffset RespondedAt)
{
    /// <summary>Projects one domain approval response into its portable JSON representation.</summary>
    /// <param name="value">The non-null terminal response to project.</param>
    /// <returns>A document carrying both unwrapped identities, the complete binding, and the projected approver identity.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonApprovalResponse FromDomain(ApprovalResponse value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonApprovalResponse(
            value.Id.Value,
            value.RequestId.Value,
            JsonApprovalScopeBinding.FromDomain(value.Binding),
            value.Resolution,
            JsonExecutionIdentity.FromDomain(value.ApproverIdentity),
            value.RespondedAt);
    }

    /// <summary>Reconstructs the exact domain response this document was projected from.</summary>
    /// <returns>A response equal to the projected original, including its binding and complete approver identity.</returns>
    /// <remarks>
    /// The approver identity is rebuilt through <see cref="JsonExecutionIdentity.ToDomain"/>, which revalidates tenant
    /// containment of the delegation chain and the assurance ceiling, so persisted approval evidence can never be edited into
    /// a stronger approver than the one the ingress authenticated.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><see cref="Binding"/> or <see cref="ApproverIdentity"/> is null, which a well-formed document never is.</exception>
    /// <exception cref="ArgumentException">The persisted binding or approver identity is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="Id"/> or <see cref="RequestId"/> is empty, or <see cref="Resolution"/> is undefined.</exception>
    public ApprovalResponse ToDomain()
    {
        ArgumentNullException.ThrowIfNull(Binding);
        ArgumentNullException.ThrowIfNull(ApproverIdentity);
        return new ApprovalResponse(
            new ApprovalResponseId(Id),
            new ApprovalRequestId(RequestId),
            Binding.ToDomain(),
            Resolution,
            ApproverIdentity.ToDomain(),
            RespondedAt);
    }
}
