// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

using AgentKit.Storage.Json;

/// <summary>Portable JSON mirror of one terminal <see cref="SecurityDecision"/>.</summary>
/// <remarks>
/// Exactly one of <see cref="Grant"/>, <see cref="Denial"/>, or <see cref="Approval"/> is populated for a well-formed
/// document, selected by <see cref="Kind"/>. Reconstruction routes through the domain constructors so tampered evidence
/// fails closed instead of minting authority.
/// </remarks>
/// <param name="Kind">The terminal decision discriminator.</param>
/// <param name="RequestId">The raw value of the evaluated security request identity.</param>
/// <param name="PolicyVersion">The positive effective policy version.</param>
/// <param name="Grant">The issued bounded grant when <paramref name="Kind"/> is <see cref="JsonSecurityDecisionKind.Allowed"/>.</param>
/// <param name="Denial">The stable denial when <paramref name="Kind"/> is <see cref="JsonSecurityDecisionKind.Denied"/>.</param>
/// <param name="Approval">The durable approval request when <paramref name="Kind"/> is <see cref="JsonSecurityDecisionKind.ApprovalRequired"/>.</param>
public sealed record JsonSecurityDecision(
    JsonSecurityDecisionKind Kind,
    Guid RequestId,
    long PolicyVersion,
    JsonSecurityGrant? Grant,
    JsonSecurityDenial? Denial,
    JsonApprovalRequest? Approval)
{
    /// <summary>Projects one domain decision into its portable JSON representation.</summary>
    /// <param name="value">The non-null decision to project.</param>
    /// <returns>A document carrying the discriminator and the applicable terminal payload.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The decision type is not one of the three closed terminal shapes.</exception>
    public static JsonSecurityDecision FromDomain(SecurityDecision value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value switch
        {
            SecurityAllowed allowed => new JsonSecurityDecision(
                JsonSecurityDecisionKind.Allowed,
                allowed.RequestId.Value,
                allowed.PolicyVersion.Value,
                JsonSecurityGrant.FromDomain(allowed.Grant),
                null,
                null),
            SecurityDenied denied => new JsonSecurityDecision(
                JsonSecurityDecisionKind.Denied,
                denied.RequestId.Value,
                denied.PolicyVersion.Value,
                null,
                JsonSecurityDenial.FromDomain(denied.Denial),
                null),
            SecurityApprovalRequired approvalRequired => new JsonSecurityDecision(
                JsonSecurityDecisionKind.ApprovalRequired,
                approvalRequired.RequestId.Value,
                approvalRequired.PolicyVersion.Value,
                null,
                null,
                JsonApprovalRequest.FromDomain(approvalRequired.Approval)),
            _ => throw new InvalidOperationException("The security decision is not a closed terminal shape."),
        };
    }

    /// <summary>Reconstructs the exact domain decision this document was projected from.</summary>
    /// <returns>A terminal decision equal to the projected original.</returns>
    /// <exception cref="InvalidOperationException">The discriminator and payload combination is inconsistent or unsupported.</exception>
    /// <exception cref="ArgumentException">A nested document is malformed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A nested identity or bound is invalid.</exception>
    public SecurityDecision ToDomain()
    {
        var requestId = new SecurityRequestId(RequestId);
        var policyVersion = new SecurityPolicyVersion(PolicyVersion);
        return Kind switch
        {
            JsonSecurityDecisionKind.Allowed when Grant is not null && Denial is null && Approval is null =>
                new SecurityAllowed(requestId, policyVersion, Grant.ToDomain()),
            JsonSecurityDecisionKind.Denied when Grant is null && Denial is not null && Approval is null =>
                new SecurityDenied(requestId, policyVersion, Denial.ToDomain()),
            JsonSecurityDecisionKind.ApprovalRequired when Grant is null && Denial is null && Approval is not null =>
                new SecurityApprovalRequired(requestId, policyVersion, Approval.ToDomain()),
            _ => throw new InvalidOperationException("The persisted security decision payload is inconsistent."),
        };
    }
}
