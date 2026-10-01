// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

/// <summary>Builds valid artifact contract values for focused constructor and invariant tests.</summary>
internal static class ArtifactContractTestData
{
    internal static AgentId AgentId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));

    internal static SessionId SessionId { get; } = new(Guid.Parse("40000000-0000-0000-0000-000000000004"));

    internal static ArtifactId ArtifactId { get; } = new(Guid.Parse("80000000-0000-0000-0000-000000000008"));

    internal static ArtifactPreparationId PreparationId { get; } = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));

    internal static InRunOperationCorrelation Correlation { get; } = new(
        new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
        new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
        null);

    internal static ExecutionIdentity Identity { get; } = TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    internal static SecurityAuthorizationScope Scope { get; } = new(AgentId, SessionId, Correlation);

    internal static ArtifactMetadata Metadata(long declaredLength = 7) => new(
        new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"),
        DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null);

    internal static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(
        id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion(version),
        new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"), new ArtifactProfileVersion(1),
        Identity.TenantId, new ArtifactOwnerId("session:owner"), Identity.PrincipalId, "text/plain", 7,
        new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch), DataClassification.Internal,
        ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null, DateTimeOffset.UnixEpoch);

    /// <summary>Creates a grant that retains the captured authorization the artifact contracts require.</summary>
    internal static SecurityGrant Grant(SecurityEffect effect, ProtectedResource resource)
    {
        var authorization = TestSupport.TestSecurityEvidence.Authorization(AgentId, SessionId, Correlation, Identity);
        return new SecurityGrant(
            new GrantId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
            new SecurityRequestId(Guid.Parse("a0000000-0000-0000-0000-00000000000a")),
            Scope, Identity, authorization, new ComponentId("artifact-store"), SecurityOperationKind.Artifact, effect,
            [resource], new InputFingerprint("fingerprint"), new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddMinutes(1), 1);
    }

    internal static SecurityGrant ArtifactGrant(SecurityEffect effect) =>
        Grant(effect, ArtifactSecurityBinding.ArtifactResource(ArtifactId));

    internal static SecurityGrant PreparationGrant(SecurityEffect effect) =>
        Grant(effect, ArtifactSecurityBinding.PreparationResource(PreparationId));
}
