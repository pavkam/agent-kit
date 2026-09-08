// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Creates complete deterministic grant-store evidence for SQLite adapter tests.</summary>
internal static class TestGrantFactory
{
    /// <summary>Creates a grant with replaceable resources and timestamp offsets.</summary>
    /// <param name="now">The validity-window start.</param>
    /// <param name="resource">The exact protected resource identifier.</param>
    /// <param name="allowedUses">The positive use count.</param>
    /// <returns>Valid grant evidence.</returns>
    internal static SecurityGrant CreateGrant(DateTimeOffset now, string resource = "/workspace/file.txt", int allowedUses = 2)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("30000000-0000-0000-0000-000000000003")),
                new RunId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
                null));
        var identity = TestSupport.TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Human);
        return new SecurityGrant(
            new GrantId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            scope,
            identity,
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

    /// <summary>Creates concrete effect evidence equal to a grant.</summary>
    /// <param name="grant">The grant to enforce.</param>
    /// <returns>Structurally matching enforcement evidence.</returns>
    internal static SecurityEnforcementRequest CreateEnforcement(SecurityGrant grant) => new(
        grant.Scope,
        grant.Identity,
        grant.Audience,
        grant.Kind,
        grant.Effect,
        grant.Resources,
        grant.InputFingerprint,
        grant.RevocationVersion);

    /// <summary>Creates a grant retaining a complete captured authorization context.</summary>
    /// <param name="now">The validity-window start.</param>
    /// <returns>Valid snapshot-bound grant evidence.</returns>
    internal static SecurityGrant CreateCapturedGrant(DateTimeOffset now)
    {
        var grant = CreateGrant(now);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("default"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                grant.PolicyVersion,
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            grant.Scope,
            grant.Identity);
        return new SecurityGrant(
            grant.Id,
            grant.RequestId,
            grant.Scope,
            grant.Identity,
            authorization,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.PolicyVersion,
            grant.RevocationVersion,
            grant.NotBefore,
            grant.ExpiresAt,
            grant.AllowedUses);
    }

    /// <summary>Creates captured enforcement evidence exactly matching a captured grant.</summary>
    /// <param name="grant">The captured grant to enforce.</param>
    /// <returns>Structurally equal snapshot-bound enforcement evidence.</returns>
    internal static SecurityEnforcementRequest CreateCapturedEnforcement(SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        var authorization = grant.Authorization;
        ArgumentNullException.ThrowIfNull(authorization);
        return new SecurityEnforcementRequest(
            grant.Scope,
            grant.Identity,
            authorization,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.RevocationVersion);
    }

    /// <summary>Creates one stable intent.</summary>
    /// <returns>A nondefault local enforcement intent.</returns>
    internal static SecurityEnforcementIntent CreateIntent() => new(
        new SecurityEnforcementIntentId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        null);
}
