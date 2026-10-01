// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Builds deterministic identities, requests, and references for coordinator tests.</summary>
internal static class ArtifactTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    internal static readonly ComponentKey<IArtifactCoordinator> CoordinatorKey = new("coordinator");
    internal static readonly ArtifactProfileKey ProfileKey = new("profile");
    internal static readonly ArtifactBackendKey BackendKey = new("backend");
    internal static readonly ArtifactDirectoryId Directory = new("tool-output");

    internal static AgentId AgentId { get; } = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));

    internal static SessionId SessionId { get; } = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));

    internal static RunId RunId { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000003"));

    internal static OperationCorrelation Correlation { get; } = new InRunOperationCorrelation(
        new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000004")), RunId, null);

    internal static ExecutionIdentity Identity { get; } = TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    internal static ExecutionIdentity OtherIdentity { get; } = TestExecutionIdentity.Create(
        new TenantId("other-tenant"), new PrincipalId("other-principal"), ExecutionSubjectKind.Human);

    internal static SecurityAuthorizationContext Authorization { get; } =
        TestSecurityEvidence.Authorization(AgentId, SessionId, Correlation, Identity);

    internal static SecurityAuthorizationContext OtherAuthorization { get; } =
        TestSecurityEvidence.Authorization(AgentId, SessionId, Correlation, OtherIdentity);

    internal static ArtifactMetadata Metadata(
        byte[] content,
        long? declaredLength = null,
        ContentHash? hash = null,
        ArtifactRetention? retention = null,
        ExternalArtifactOwnership? external = null,
        ArtifactMutability mutability = ArtifactMutability.Immutable,
        bool omitHash = false) => new(
            new ArtifactOwnerId("session:owner"),
            "text/plain",
            declaredLength ?? content.LongLength,
            omitHash ? null : hash ?? FileSecurityBinding.ContentFingerprint(content),
            DataClassification.Internal,
            external is null ? ArtifactOwnershipKind.Session : ArtifactOwnershipKind.External,
            external is null ? mutability : ArtifactMutability.ExternallyManaged,
            retention ?? new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false),
            external);

    internal static ArtifactPrepareRequest Prepare(
        byte[] content,
        ArtifactMetadata? metadata = null,
        SecurityAuthorizationContext? authorization = null,
        ArtifactDirectoryId? directory = null,
        string key = "prepare-1") => new(
            AgentId,
            SessionId,
            null,
            Correlation,
            authorization ?? Authorization,
            directory ?? Directory,
            metadata ?? Metadata(content),
            new MemoryStream(content, writable: false),
            new IdempotencyKey(key));

    internal static ArtifactFinalizeRequest Finalize(ArtifactPreparationId preparationId, SecurityAuthorizationContext? authorization = null, string key = "finalize-1") => new(
        preparationId, AgentId, SessionId, null, Correlation, authorization ?? Authorization, new IdempotencyKey(key));

    internal static ArtifactAbortRequest Abort(ArtifactPreparationId preparationId, SecurityAuthorizationContext? authorization = null, string key = "abort-1") => new(
        preparationId, AgentId, SessionId, Correlation, authorization ?? Authorization, ArtifactAbortReason.Cancelled, new IdempotencyKey(key));

    internal static ArtifactReadRequest Read(ArtifactReference reference, SecurityAuthorizationContext? authorization = null) => new(
        AgentId, SessionId, null, Correlation, authorization ?? Authorization, reference);

    internal static ArtifactDeleteRequest Delete(ArtifactReference reference, SecurityAuthorizationContext? authorization = null, string key = "delete-1") => new(
        AgentId, SessionId, null, Correlation, authorization ?? Authorization, reference, new IdempotencyKey(key));

    internal static ArtifactReconciliationRequest Reconcile(ArtifactPreparationId preparationId, SecurityAuthorizationContext? authorization = null, string key = "reconcile-1") => new(
        preparationId, AgentId, SessionId, Correlation, authorization ?? Authorization, new IdempotencyKey(key));
}
