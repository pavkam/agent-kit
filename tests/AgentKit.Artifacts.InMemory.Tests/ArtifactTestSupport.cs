// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

/// <summary>Builds valid store requests and entries for tests of the shared planner, state, backend, and intent store.</summary>
internal static class ArtifactTestSupport
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    internal static ExecutionIdentity Identity { get; } = TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    internal static ExecutionIdentity Other { get; } = TestExecutionIdentity.Create(
        new TenantId("other"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human);

    internal static ArtifactPreparationId Preparation(int n) => new(new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]));

    internal static ArtifactId Artifact(int n) => new(new Guid(n, 1, 0, [0, 0, 0, 0, 0, 0, 0, 2]));

    internal static ArtifactMetadata Metadata(byte[] content, ArtifactRetention? retention = null, ExternalArtifactOwnership? external = null, ContentHash? declared = null) => new(
        new ArtifactOwnerId("owner"), "text/plain", content.LongLength, declared ?? FileSecurityBinding.ContentFingerprint(content),
        DataClassification.Internal, external is null ? ArtifactOwnershipKind.Session : ArtifactOwnershipKind.External,
        external is null ? ArtifactMutability.Immutable : ArtifactMutability.ExternallyManaged,
        retention ?? new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), external);

    internal static SecurityGrant Grant(ExecutionIdentity identity, SecurityEffect effect, ProtectedResource resource)
    {
        var scope = new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new InRunOperationCorrelation(new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), new RunId(Guid.Parse("30000000-0000-0000-0000-000000000003")), null));
        var authorization = TestSecurityEvidence.Authorization(scope.AgentId, scope.SessionId, scope.Correlation, identity);
        return new SecurityGrant(
            new GrantId(Guid.NewGuid()), new SecurityRequestId(Guid.NewGuid()), scope, identity, authorization, new ComponentId("test.artifacts"),
            SecurityOperationKind.Artifact, effect, [resource], new InputFingerprint("fingerprint"), new SecurityPolicyVersion(1),
            new SecurityRevocationVersion(1), Now, Now.AddHours(1), 1);
    }

    internal static ArtifactStorePrepareRequest Prepare(
        byte[] content, int preparation = 1, int artifact = 1, string key = "key", ExecutionIdentity? identity = null,
        ArtifactMetadata? metadata = null, TimeSpan? lifetime = null, string version = "1")
    {
        var who = identity ?? Identity;
        var grant = Grant(who, SecurityEffect.Create, ArtifactSecurityBinding.ArtifactResource(Artifact(artifact)));
        return new ArtifactStorePrepareRequest(
            Artifact(artifact), Preparation(preparation), new ArtifactVersion(version), new ArtifactProfileKey("profile"), new ArtifactProfileVersion(1),
            who.TenantId, who.PrincipalId, new ArtifactDirectoryId("dir"), metadata ?? Metadata(content), [.. content],
            FileSecurityBinding.ContentFingerprint(content), Now, Now + (lifetime ?? TimeSpan.FromMinutes(5)), grant, new IdempotencyKey(key));
    }

    internal static ArtifactStoreFinalizeRequest Publish(int preparation = 1, ExecutionIdentity? identity = null) => new(
        Preparation(preparation), Grant(identity ?? Identity, SecurityEffect.CreateOrReplace, ArtifactSecurityBinding.PreparationResource(Preparation(preparation))),
        new IdempotencyKey($"finalize-{Guid.NewGuid()}"));

    internal static ArtifactStoreAbortRequest Abort(int preparation = 1, ExecutionIdentity? identity = null) => new(
        Preparation(preparation), ArtifactAbortReason.Cancelled,
        Grant(identity ?? Identity, SecurityEffect.Delete, ArtifactSecurityBinding.PreparationResource(Preparation(preparation))),
        new IdempotencyKey($"abort-{Guid.NewGuid()}"));

    internal static ArtifactStoreReadRequest Read(ArtifactReference reference, ExecutionIdentity? identity = null) => new(
        reference, Grant(identity ?? Identity, SecurityEffect.Observe, ArtifactSecurityBinding.ArtifactResource(reference.Id)));

    internal static ArtifactStoreDeleteRequest Delete(ArtifactReference reference, ExecutionIdentity? identity = null) => new(
        reference, Grant(identity ?? Identity, SecurityEffect.Delete, ArtifactSecurityBinding.ArtifactResource(reference.Id)),
        new IdempotencyKey($"delete-{Guid.NewGuid()}"));
}
