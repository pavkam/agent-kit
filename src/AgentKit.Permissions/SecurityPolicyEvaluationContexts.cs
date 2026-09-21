// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Builds <see cref="SecurityPolicyContext"/> values for authority evaluation.</summary>
internal static class SecurityPolicyEvaluationContexts
{
    /// <summary>Creates evaluation evidence for one request.</summary>
    /// <param name="request">The request being evaluated.</param>
    /// <param name="policyVersion">The authority's active policy version.</param>
    /// <param name="policySnapshot">The authority's configured snapshot reference, if any.</param>
    /// <param name="revocationVersion">The authority's live revocation epoch.</param>
    /// <param name="evaluatedAt">The instant evaluation began.</param>
    /// <returns>A context suitable for <see cref="ISecurityPolicy.EvaluateAsync"/>.</returns>
    internal static SecurityPolicyContext Create(
        SecurityRequest request,
        SecurityPolicyVersion policyVersion,
        SecurityPolicySnapshotReference? policySnapshot,
        SecurityRevocationVersion revocationVersion,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Authorization is { } captured)
        {
            return new SecurityPolicyContext(captured, revocationVersion, evaluatedAt);
        }

        ArgumentNullException.ThrowIfNull(request.Scope);
        ArgumentNullException.ThrowIfNull(request.Identity);
        var snapshot = policySnapshot ?? CreateUncapturedReference(policyVersion);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("default"),
            new SecurityProfileVersion(1),
            snapshot,
            new ComponentKey<ISecurityAuthority>("default"),
            new AgentDefinitionRevision(0),
            new ConfigurationVersion(1),
            request.Scope,
            request.Identity);
        return new SecurityPolicyContext(authorization, revocationVersion, evaluatedAt);
    }

    /// <summary>Creates the synthetic snapshot reference used for uncaptured authorization evidence.</summary>
    /// <param name="version">The positive policy version bound to the synthetic snapshot.</param>
    /// <returns>A stable uncaptured snapshot reference.</returns>
    internal static SecurityPolicySnapshotReference CreateUncapturedReference(SecurityPolicyVersion version) =>
        new(
            new SecurityPolicySnapshotId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            version,
            new ContentHash("sha256:uncaptured"));
}
