// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies <see cref="ArtifactProcessOutputSink"/> binds complete process output to one keyed coordinator.</summary>
public sealed class ArtifactProcessOutputSinkTests
{
    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var options = AgentArtifactOptionsSnapshot.Create(new AgentArtifactOptions());
        using var harness = CoordinatorHarness.Create();

        Should.Throw<ArgumentNullException>(() => new ArtifactProcessOutputSink(null!, ArtifactTestData.Directory, options)).ParamName.ShouldBe("artifacts");
        Should.Throw<ArgumentException>(() => new ArtifactProcessOutputSink(harness.Coordinator, default, options)).ParamName.ShouldBe("defaultDirectory");
        Should.Throw<ArgumentNullException>(() => new ArtifactProcessOutputSink(harness.Coordinator, ArtifactTestData.Directory, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task StoreAsync_WhenPublicationSucceeds_ReturnsAReadableReferenceWithCompleteBytes()
    {
        using var harness = CoordinatorHarness.Create();
        var sink = harness.Services.GetRequiredService<IProcessOutputArtifactSink>();
        var request = CreateRequest("complete output"u8.ToArray());

        var result = await sink.StoreAsync(request, TestContext.Current.CancellationToken);

        var reference = result.ShouldBeOfType<ProcessOutputArtifactStored>().Reference;
        reference.Length.ShouldBe(request.Content.Length);
        reference.Integrity.ContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(request.Content.AsSpan()));
        reference.OwnerId.ShouldBe(new ArtifactOwnerId($"session:{ArtifactTestData.SessionId}"));
        reference.Ownership.ShouldBe(ArtifactOwnershipKind.Session);
        reference.Classification.ShouldBe(DataClassification.Internal);
        reference.Retention.Policy.ShouldBe(new ArtifactRetentionPolicyKey("session"));
        reference.DirectoryId.ShouldBe(ArtifactTestData.Directory);
        (await harness.ReadTextAsync(reference)).ShouldBe("complete output");
    }

    [Fact]
    public async Task StoreAsync_WhenTheOptionsNameADirectory_UsesThatDirectoryRatherThanTheProfileDefault()
    {
        using var harness = CoordinatorHarness.Create(
            options => options.ProcessOutputDirectory = new ArtifactDirectoryId("proc"),
            profile => profile.Routes[new ArtifactDirectoryId("proc")] = ArtifactTestData.BackendKey);
        var sink = harness.Services.GetRequiredService<IProcessOutputArtifactSink>();

        var result = await sink.StoreAsync(CreateRequest("out"u8.ToArray()), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProcessOutputArtifactStored>().Reference.DirectoryId.ShouldBe(new ArtifactDirectoryId("proc"));
    }

    [Fact]
    public async Task StoreAsync_WhenTheRunHasNoSession_OwnsTheContentByRun()
    {
        using var harness = CoordinatorHarness.Create();
        var sink = harness.Services.GetRequiredService<IProcessOutputArtifactSink>();
        var scope = new SecurityAuthorizationScope(ArtifactTestData.AgentId, null, ArtifactTestData.Correlation);
        var authorization = TestSecurityEvidence.Authorization(ArtifactTestData.AgentId, null, ArtifactTestData.Correlation, ArtifactTestData.Identity);
        var request = new ProcessOutputArtifactRequest(
            ProcessTestIntent.Create(), scope, ArtifactTestData.Identity, authorization, ProcessOutputKind.StandardOutput,
            [.. "out"u8.ToArray()], new IdempotencyKey("process-output:run"));

        var reference = (await sink.StoreAsync(request, TestContext.Current.CancellationToken)).ShouldBeOfType<ProcessOutputArtifactStored>().Reference;

        reference.Ownership.ShouldBe(ArtifactOwnershipKind.Run);
        reference.OwnerId.ShouldBe(new ArtifactOwnerId($"run:{ArtifactTestData.RunId}"));
    }

    [Fact]
    public async Task StoreAsync_WhenPreparationFails_ReturnsRejectedWithoutFinalizingOrAborting()
    {
        using var harness = CoordinatorHarness.Create(options => options.MaximumArtifactBytes = 2);
        var sink = harness.Services.GetRequiredService<IProcessOutputArtifactSink>();

        var result = await sink.StoreAsync(CreateRequest("too long"u8.ToArray()), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ProcessOutputArtifactRejected>().SafeMessage.ShouldContain("byte limit");
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task StoreAsync_WhenFinalizationIsDenied_AbortsTheUnpublishedPreparation()
    {
        using var harness = CoordinatorHarness.Create();
        var sink = harness.Services.GetRequiredService<IProcessOutputArtifactSink>();
        harness.Authority.OnAuthorize = request =>
        {
            harness.Authority.Deny = request.Effect == SecurityEffect.CreateOrReplace;
            return Task.CompletedTask;
        };

        var result = await sink.StoreAsync(CreateRequest("output"u8.ToArray()), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ProcessOutputArtifactRejected>();
        harness.Sink.Events.OfType<ArtifactAbortedEvent>().ShouldHaveSingleItem().Reason.ShouldBe(ArtifactAbortReason.ReferenceCommitFailure);
    }

    [Fact]
    public async Task StoreAsync_WhenTheRequestIsNull_ThrowsNamingIt()
    {
        using var harness = CoordinatorHarness.Create();
        var sink = harness.Services.GetRequiredService<IProcessOutputArtifactSink>();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await sink.StoreAsync(null!))).ParamName.ShouldBe("request");
    }

    private static ProcessOutputArtifactRequest CreateRequest(byte[] bytes)
    {
        var scope = new SecurityAuthorizationScope(ArtifactTestData.AgentId, ArtifactTestData.SessionId, ArtifactTestData.Correlation);
        return new ProcessOutputArtifactRequest(
            ProcessTestIntent.Create(), scope, ArtifactTestData.Identity, ArtifactTestData.Authorization, ProcessOutputKind.StandardOutput,
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
