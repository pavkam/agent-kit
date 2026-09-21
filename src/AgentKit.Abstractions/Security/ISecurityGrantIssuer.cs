// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Mints bounded security grants from an already-decided allow or approved scope.</summary>
/// <remarks>The issuer alone assembles grant evidence; it never evaluates policy and never registers the grant with <see cref="ISecurityGrantStore"/>. The authority that owns the decision performs registration after issuance so both steps stay observable and separately testable.</remarks>
public interface ISecurityGrantIssuer
{
    /// <summary>Issues a bounded grant for a request the authority has already decided to allow.</summary>
    /// <param name="request">The complete normalized request being granted.</param>
    /// <param name="policyVersion">The effective policy version that decided the request.</param>
    /// <param name="revocationVersion">The live revocation epoch captured at issue time.</param>
    /// <param name="approvedBinding">The exact human-approved scope, or null when the request was allowed without approval.</param>
    /// <param name="approvalResponseId">The terminal approval response identity when approval bound the grant.</param>
    /// <param name="cancellationToken">Cancels issuance before it completes.</param>
    /// <returns>The freshly minted, not-yet-registered grant.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="approvedBinding"/> is present and does not bind the same request.</exception>
    public ValueTask<SecurityGrant> IssueAsync(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        SecurityRevocationVersion revocationVersion,
        ApprovalScopeBinding? approvedBinding,
        ApprovalResponseId? approvalResponseId = null,
        CancellationToken cancellationToken = default);
}
