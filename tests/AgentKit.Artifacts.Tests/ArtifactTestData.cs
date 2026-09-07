// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

internal static class ArtifactTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    internal static AgentId AgentId { get; } = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    internal static SessionId SessionId { get; } = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    internal static RunId RunId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    internal static OperationCorrelation Correlation { get; } = new InRunOperationCorrelation(
        new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), RunId, null);
    internal static ExecutionIdentity Identity { get; } = new(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human, ExtensionData.Empty);
    internal static ArtifactId ArtifactId { get; } = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    internal static ArtifactPreparationId PreparationId { get; } = new(Guid.Parse("60000000-0000-0000-0000-000000000006"));
    internal static SecurityRequestId SecurityRequestId { get; } = new(Guid.Parse("70000000-0000-0000-0000-000000000007"));

    internal static ArtifactPrepareRequest CreatePrepare(byte[] content, long? declaredLength = null, ContentHash? hash = null) => new(
        AgentId,
        SessionId,
        null,
        Correlation,
        Identity,
        new ArtifactDirectoryId("tool-output"),
        new ArtifactMetadata(
            new ArtifactOwnerId("session:owner"),
            "text/plain",
            declaredLength ?? content.LongLength,
            hash ?? FileSecurityBinding.ContentFingerprint(content),
            ArtifactDataClassification.Internal,
            ArtifactOwnershipKind.Session,
            ArtifactMutability.Immutable,
            new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false)),
        new MemoryStream(content, writable: false),
        new IdempotencyKey("prepare-1"));

    internal static ArtifactReference CreateReference(bool legalHold = false)
    {
        var hash = FileSecurityBinding.ContentFingerprint("content"u8);
        return new ArtifactReference(
            ArtifactId, new ArtifactVersion("1"), new ArtifactDirectoryId("tool-output"),
            new ArtifactProfileKey("test"), new ArtifactProfileVersion(1), Identity.TenantId,
            new ArtifactOwnerId("session:owner"), Identity.PrincipalId, "text/plain", 7,
            new ArtifactIntegrity(hash, Now), ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session,
            ArtifactMutability.Immutable, new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, legalHold), Now);
    }
}
