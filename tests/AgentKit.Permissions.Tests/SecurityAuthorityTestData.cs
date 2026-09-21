// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using Microsoft.Extensions.Options;

/// <summary>Creates valid request evidence shared by authority tests without selecting a concrete grant-store adapter.</summary>
internal static class SecurityAuthorityTestData
{
    /// <summary>Creates a valid security request with deterministic identity, scope, and bounded expiry.</summary>
    /// <param name="now">The optional issue instant used to derive the exclusive request deadline.</param>
    /// <returns>One request whose resource and input values are valid but do not grant authority.</returns>
    internal static SecurityRequest CreateRequest(DateTimeOffset? now = null)
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
        return new SecurityRequest(
            new SecurityRequestId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            scope,
            null,
            identity,
            new ComponentId("filesystem"),
            SecurityOperationKind.FileRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.File, "/workspace/file.txt")],
            new InputFingerprint("sha256:abc"),
            (now ?? DateTimeOffset.UnixEpoch).AddMinutes(10));
    }

    /// <summary>Creates policy evaluation evidence for one request.</summary>
    /// <param name="request">The request under evaluation.</param>
    /// <param name="evaluatedAt">The instant evaluation began.</param>
    /// <returns>A context suitable for direct policy evaluation in tests.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    internal static SecurityPolicyContext PolicyContext(SecurityRequest request, DateTimeOffset? evaluatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var at = evaluatedAt ?? new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        if (request.Authorization is { } captured)
        {
            return new SecurityPolicyContext(captured, new SecurityRevocationVersion(1), at);
        }

        var snapshot = new SecurityPolicySnapshotReference(
            new SecurityPolicySnapshotId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            new SecurityPolicyVersion(1),
            new ContentHash("sha256:test"));
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("default"),
            new SecurityProfileVersion(1),
            snapshot,
            new ComponentKey<ISecurityAuthority>("default"),
            new AgentDefinitionRevision(0),
            new ConfigurationVersion(1),
            request.Scope,
            request.Identity);
        return new SecurityPolicyContext(authorization, new SecurityRevocationVersion(1), at);
    }

    /// <summary>Creates the default policy selector for one frozen permission-options snapshot.</summary>
    /// <param name="options">The permission options whose policy snapshot is retained by the catalog.</param>
    /// <param name="publications">Optional profile publications whose snapshots are also retained.</param>
    /// <returns>A selector backed by a catalog constructed from the supplied evidence.</returns>
    internal static ISecurityPolicySelector CreatePolicySelector(
        AgentPermissionOptions options,
        IEnumerable<SecurityProfilePublication>? publications = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var catalog = new SecurityPolicyCatalog(
            Options.Create(options),
            publications ?? []);
        return new DefaultSecurityPolicySelector(catalog, Options.Create(options));
    }

    /// <summary>Creates a valid, non-expired approval request bound to a request from <see cref="CreateRequest"/>.</summary>
    /// <param name="now">The optional issue instant used to derive the request and its bounded expiry.</param>
    /// <returns>One approval request suitable for exercising approval-handler and broker behavior.</returns>
    internal static ApprovalRequest CreateApprovalRequest(DateTimeOffset? now = null)
    {
        var issued = now ?? DateTimeOffset.UnixEpoch;
        var securityRequest = CreateRequest(issued);
        var binding = new ApprovalScopeBinding(
            securityRequest,
            new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1),
            issued,
            issued.AddMinutes(5),
            1);
        return new ApprovalRequest(
            new ApprovalRequestId(Guid.Parse("41000000-0000-0000-0000-000000000004")),
            binding,
            "Approve a bounded test operation.",
            issued);
    }
}
