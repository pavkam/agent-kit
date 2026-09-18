// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Creates complete deterministic grant, enforcement, intent, and receipt evidence for the JSON adapter cases.</summary>
/// <remarks>
/// Every value uses fixed identities and caller-supplied instants, so a case never depends on a clock, a random source, or
/// the host locale. Grant evidence is structurally exact: the enforcement builders reproduce the grant's own scope,
/// identity, audience, resources, and fingerprint so a case that wants a mismatch has to introduce it deliberately.
/// </remarks>
internal static class TestGrantFactory
{
    /// <summary>Creates one valid grant with replaceable identity, resource, and use bounds.</summary>
    /// <param name="now">The validity-window start; the grant expires ten minutes later.</param>
    /// <param name="resource">The exact protected resource identifier.</param>
    /// <param name="allowedUses">The positive total use budget.</param>
    /// <param name="grantId">The optional grant identity; defaults to a fixed deterministic value.</param>
    /// <param name="requestId">The optional originating request identity; defaults to a fixed deterministic value.</param>
    /// <param name="identity">The optional execution identity; defaults to a minimal test identity without claims or delegation.</param>
    /// <returns>Valid grant evidence that the JSON store accepts.</returns>
    internal static SecurityGrant CreateGrant(
        DateTimeOffset now,
        string resource = "/workspace/file.txt",
        int allowedUses = 2,
        GrantId? grantId = null,
        SecurityRequestId? requestId = null,
        ExecutionIdentity? identity = null)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null));
        return new SecurityGrant(
            grantId ?? new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            requestId ?? new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            scope,
            identity ?? TestExecutionIdentity.Create(
                new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            new ComponentId("filesystem"),
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.File, resource)],
            new InputFingerprint("sha256:abc"),
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            now,
            now + TimeSpan.FromMinutes(10),
            allowedUses);
    }

    /// <summary>Creates concrete effect evidence structurally equal to a grant.</summary>
    /// <param name="grant">The grant whose exact effect is being enforced.</param>
    /// <returns>Enforcement evidence the store accepts as matching.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    internal static SecurityEnforcementRequest CreateEnforcement(SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return new SecurityEnforcementRequest(
            grant.Scope,
            grant.Identity,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.RevocationVersion);
    }

    /// <summary>Creates one deterministic enforcement intent.</summary>
    /// <param name="seed">The nonzero seed distinguishing independent intents within a case.</param>
    /// <param name="fence">The optional required fencing token retained in the resulting receipt.</param>
    /// <returns>A nondefault local enforcement intent.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is not positive.</exception>
    internal static SecurityEnforcementIntent CreateIntent(int seed = 1, FencingToken? fence = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seed);
        return new SecurityEnforcementIntent(
            new SecurityEnforcementIntentId(new Guid(seed, 0, 0, [0, 0, 0, 0, 0, 0, 0, 7])),
            fence);
    }

    /// <summary>Creates the receipt the store would persist for one consumed intent.</summary>
    /// <param name="grant">The consumed grant.</param>
    /// <param name="enforcement">The exact enforced effect.</param>
    /// <param name="intent">The presented enforcement intent.</param>
    /// <param name="recordedAt">The instant the store recorded the decision.</param>
    /// <returns>Receipt evidence whose fingerprint binds the effect to the intent.</returns>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    internal static SecurityEnforcementIntentReceipt CreateReceipt(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        return new SecurityEnforcementIntentReceipt(
            intent.Id,
            grant.Id,
            grant.RequestId,
            enforcement,
            intent.RequiredFence,
            SecurityEnforcementBinding.Fingerprint(enforcement, intent),
            recordedAt);
    }

    /// <summary>Creates an identity carrying claims, a delegation chain, and expiring authentication evidence.</summary>
    /// <param name="now">The authentication instant used to derive a later expiry.</param>
    /// <returns>An identity that exercises every optional branch of the persisted identity mirror.</returns>
    internal static ExecutionIdentity CreateRichIdentity(DateTimeOffset now)
    {
        var issuer = new IdentityIssuerId("issuer");
        var claim = new IdentityClaim(issuer, "role", "admin", IdentityClaimValueKind.Text);
        return new ExecutionIdentity(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human,
            new AuthenticationEvidence(
                new AuthenticationEvidenceId("evidence"),
                issuer,
                "password",
                now,
                now.AddMinutes(30),
                new AuthenticationEvidenceFingerprint(new ContentHash("sha256:fingerprint"))),
            [claim],
            [
                new DelegationIdentityLink(
                    new DelegationId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                    new TenantId("tenant"),
                    new PrincipalId("parent-principal"),
                    issuer,
                    new AuthenticationEvidenceId("parent-evidence"),
                    new IdentityVersion(1),
                    now,
                    [claim],
                    IdentityAssuranceLevel.HardwareBacked),
            ],
            IdentityAssuranceLevel.HardwareBacked,
            new IdentityVersion(1));
    }
}
