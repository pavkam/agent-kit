// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;

public sealed class ArtifactContractsTests
{
    [Fact]
    public void ArtifactMetadata_WhenLengthIsNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Metadata(declaredLength: -1));

        exception.ParamName.ShouldBe("declaredLength");
    }

    [Fact]
    public void ArtifactReference_WhenIdentityIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => Reference(id: default(ArtifactId)));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void ArtifactPrepareRequest_WhenStreamIsUnreadable_ThrowsExactParameter()
    {
        var stream = new MemoryStream();
        stream.Dispose();

        var exception = Should.Throw<ArgumentException>(() => new ArtifactPrepareRequest(
            AgentId(), SessionId(), null, Correlation(), Identity(), new ArtifactDirectoryId("output"),
            Metadata(), stream, new IdempotencyKey("prepare")));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ArtifactFinalizeRequest_WhenPreparationIdentityIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactFinalizeRequest(
            default, AgentId(), SessionId(), null, Correlation(), Identity(), new IdempotencyKey("finalize")));

        exception.ParamName.ShouldBe("preparationId");
    }

    [Fact]
    public void ArtifactAbortRequest_WhenReasonIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactAbortRequest(
            PreparationId(), AgentId(), SessionId(), Correlation(), Identity(), (ArtifactAbortReason) 999,
            new IdempotencyKey("abort")));

        exception.ParamName.ShouldBe("reason");
    }

    [Fact]
    public void ArtifactDeleteRequest_WhenIdempotencyKeyIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ArtifactDeleteRequest(
            AgentId(), SessionId(), null, Correlation(), Identity(), Reference(), default));

        exception.ParamName.ShouldBe("idempotencyKey");
    }

    [Fact]
    public async Task ArtifactReadOpened_WhenDisposed_DisposesOwnedStream()
    {
        var stream = new MemoryStream("content"u8.ToArray());
        var opened = new ArtifactReadOpened(Reference(), stream);

        await opened.DisposeAsync();

        stream.CanRead.ShouldBeFalse();
    }

    [Fact]
    public void ArtifactSecurityBinding_WhenVersionChanges_ChangesReadAndDeleteEvidence()
    {
        var first = Reference(version: "1");
        var second = Reference(version: "2");

        ArtifactSecurityBinding.ReadFingerprint(first).ShouldNotBe(ArtifactSecurityBinding.ReadFingerprint(second));
        ArtifactSecurityBinding.DeleteFingerprint(first).ShouldNotBe(ArtifactSecurityBinding.DeleteFingerprint(second));
    }

    [Fact]
    public void ProcessOutputArtifactRequest_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ProcessOutputArtifactRequest(
            Intent(), Scope(), Identity(), ProcessOutputKind.StandardOutput, default, new IdempotencyKey("output")));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ProcessRunResult_WhenArtifactReferencesMatch_IsStructurallyEqual()
    {
        var reference = Reference();
        var left = Result(reference);
        var right = Result(reference);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    private static ProcessRunResult Result(ArtifactReference reference) => new(
        ProcessRunStatus.Exited, 0, [], [], 10, 0, true, false,
        ProcessSideEffectCertainty.Completed, null, reference);

    private static ArtifactMetadata Metadata(long declaredLength = 7) => new(
        new ArtifactOwnerId("session:owner"), "text/plain", declaredLength, new ContentHash("hash"),
        ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false));

    private static ArtifactReference Reference(ArtifactId? id = null, string version = "1") => new(
        id ?? new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new ArtifactVersion(version), new ArtifactDirectoryId("output"), new ArtifactProfileKey("test"),
        new ArtifactProfileVersion(1), Identity().TenantId, new ArtifactOwnerId("session:owner"),
        Identity().PrincipalId, "text/plain", 7, new ArtifactIntegrity(new ContentHash("hash"), DateTimeOffset.UnixEpoch),
        ArtifactDataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
        new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), DateTimeOffset.UnixEpoch);

    private static ResolvedProcessIntent Intent()
    {
        var request = new ProcessResolveRequest(
            new ProcessOperationId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            "/bin/sh", [], null, [], [], new SandboxProfileId("test"), ProcessWorkspaceAccess.ReadOnly,
            ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny,
            new ProcessResourceLimits(TimeSpan.FromSeconds(1), 10, TimeSpan.FromMilliseconds(10)));
        return new ResolvedProcessIntent(
            request, "/bin/sh", new ContentHash("executable"), "/workspace", "/workspace",
            new ContentHash("environment"), new ContentHash("input"));
    }

    private static SecurityAuthorizationScope Scope() => new(AgentId(), SessionId(), Correlation());
    private static AgentId AgentId() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
    private static SessionId SessionId() => new(Guid.Parse("40000000-0000-0000-0000-000000000004"));
    private static ArtifactPreparationId PreparationId() => new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    private static InRunOperationCorrelation Correlation() => new(
        new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
        new RunId(Guid.Parse("70000000-0000-0000-0000-000000000007")), null);
    private static ExecutionIdentity Identity() => AgentKit.TestSupport.TestExecutionIdentity.Create(
        new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
}
