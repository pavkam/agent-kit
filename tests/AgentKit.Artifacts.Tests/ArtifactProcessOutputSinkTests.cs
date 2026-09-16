// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

public sealed class ArtifactProcessOutputSinkTests
{
    [Fact]
    public async Task StoreAsync_WhenPublicationSucceeds_ReturnsReferenceAndPreservesCompleteBytes()
    {
        var artifacts = new RecordingArtifactCoordinator();
        var sink = new ArtifactProcessOutputSink(artifacts, Options.Create(new AgentArtifactOptions()));
        var request = CreateRequest("complete output"u8.ToArray());

        var result = await sink.StoreAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProcessOutputArtifactStored>().Reference.ShouldBeSameAs(
            artifacts.FinalizeResult.ShouldBeOfType<ArtifactFinalized>().Reference);
        var prepare = artifacts.PrepareRequests.ShouldHaveSingleItem();
        prepare.Metadata.DeclaredLength.ShouldBe(request.Content.Length);
        prepare.Metadata.DeclaredContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(request.Content.AsSpan()));
        artifacts.PreparedContent.ShouldBe(request.Content);
        artifacts.FinalizeRequests.ShouldHaveSingleItem().PreparationId.ShouldBe(ArtifactTestData.PreparationId);
    }

    [Fact]
    public async Task StoreAsync_WhenPreparationFails_ReturnsRejectedWithoutFinalizingOrAborting()
    {
        var artifacts = new RecordingArtifactCoordinator
        {
            PrepareResult = new ArtifactPrepareRejected(new ArtifactFailure(ArtifactFailureKind.LimitExceeded, "Too large.")),
        };
        var sink = new ArtifactProcessOutputSink(artifacts, Options.Create(new AgentArtifactOptions()));

        var result = await sink.StoreAsync(CreateRequest("output"u8.ToArray()), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProcessOutputArtifactRejected>().SafeMessage.ShouldBe("Too large.");
        artifacts.FinalizeRequests.ShouldBeEmpty();
        artifacts.AbortRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreAsync_WhenFinalizationFails_AbortsUnpublishedPreparation()
    {
        var artifacts = new RecordingArtifactCoordinator
        {
            FinalizeResult = new ArtifactFinalizeRejected(new ArtifactFailure(ArtifactFailureKind.Unavailable, "Unavailable.")),
        };
        var sink = new ArtifactProcessOutputSink(artifacts, Options.Create(new AgentArtifactOptions()));

        var result = await sink.StoreAsync(CreateRequest("output"u8.ToArray()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ProcessOutputArtifactRejected>();
        artifacts.AbortRequests.ShouldHaveSingleItem().Reason.ShouldBe(ArtifactAbortReason.ReferenceCommitFailure);
    }

    private static ProcessOutputArtifactRequest CreateRequest(byte[] bytes)
    {
        var scope = new SecurityAuthorizationScope(
            ArtifactTestData.AgentId, ArtifactTestData.SessionId, ArtifactTestData.Correlation);
        return new ProcessOutputArtifactRequest(
            ProcessTestIntent.Create(), scope, ArtifactTestData.Identity, ProcessOutputKind.StandardOutput,
            [.. bytes], new IdempotencyKey("process-output:test"));
    }

    private static class ProcessTestIntent
    {
        internal static ResolvedProcessIntent Create()
        {
            var request = new ProcessResolveRequest(
                new ProcessOperationId(Guid.Parse("90000000-0000-0000-0000-000000000009")),
                "/bin/echo", ["hello"], null, [], [], new SandboxProfileId("test"),
                ProcessWorkspaceAccess.ReadOnly, ProcessSideEffectClass.ReadOnly, ProcessChildPolicy.Deny,
                new ProcessResourceLimits(TimeSpan.FromSeconds(1), 4, TimeSpan.FromMilliseconds(10)));
            return new ResolvedProcessIntent(
                request, "/bin/echo", new ContentHash("executable"), "/workspace", "/workspace",
                new ContentHash("environment"), new ContentHash("input"));
        }
    }
}
