// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

using static ArtifactTestSupport;

/// <summary>Verifies the shared state backend's commit order, failure mapping, initialization, and disposal.</summary>
public sealed class ArtifactStateBackendTests
{
    private static readonly byte[] _content = "payload"u8.ToArray();

    [Fact]
    public void Constructor_WhenAdapterIsBlank_ThrowsNamingIt() =>
        Should.Throw<ArgumentException>(() => new BlankAdapterBackend()).ParamName.ShouldBe("adapter");

    [Fact]
    public async Task Initialization_WhenRepeatedAcrossOperations_RecoversExactlyOnce()
    {
        using var backend = new FaultInjectingArtifactBackend();

        await backend.InitializeForTestAsync(TestContext.Current.CancellationToken);
        await backend.InitializeForTestAsync(TestContext.Current.CancellationToken);
        _ = await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken);

        backend.Recoveries.ShouldBe(1);
    }

    [Fact]
    public async Task PrepareAsync_WhenRecoveryFails_SurfacesTheFailureAndRetriesRecoveryNextTime()
    {
        using var backend = new FaultInjectingArtifactBackend { RecoverFailure = new InvalidOperationException("corrupt") };

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken));
        backend.RecoverFailure = null;
        var result = await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ArtifactStorePrepared>();
        backend.Recoveries.ShouldBe(2);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("persist")]
    public async Task PrepareAsync_WhenDurableWriteFails_ReportsUnavailableAndLeavesNoStateOrPayload(string failing)
    {
        using var backend = new FaultInjectingArtifactBackend();
        if (failing == "stage")
        {
            backend.StageFailure = new IOException("disk");
        }
        else
        {
            backend.PersistFailure = new UnauthorizedAccessException("denied");
        }

        var failed = await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken);
        backend.StageFailure = null;
        backend.PersistFailure = null;
        var retried = await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken);

        failed.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        _ = retried.ShouldBeOfType<ArtifactStorePrepared>();
        backend.PayloadCount.ShouldBe(1);
    }

    [Fact]
    public async Task PrepareAsync_WhenPersistFailsAfterStaging_ReleasesTheStagedPayload()
    {
        using var backend = new FaultInjectingArtifactBackend { PersistFailure = new IOException("disk") };

        _ = await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken);

        backend.PayloadCount.ShouldBe(0);
        backend.Releases.ShouldBe([false]);
    }

    [Fact]
    public async Task PrepareAsync_WhenCancelledAfterStaging_ReleasesThePayloadAndRethrows()
    {
        using var cancellation = new CancellationTokenSource();
        using var backend = new FaultInjectingArtifactBackend { CancelDuringPersist = cancellation };

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await backend.PrepareAsync(Prepare(_content), Now, cancellation.Token));

        backend.PayloadCount.ShouldBe(0);
    }

    [Fact]
    public async Task AbortAsync_WhenReleaseFailsAfterTheEntryWasPersisted_StillReportsTheAbort()
    {
        var logger = new RecordingLogger<FaultInjectingArtifactBackend>();
        using var backend = new FaultInjectingArtifactBackend(logger);
        _ = await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken);
        backend.ReleaseFailure = new IOException("disk");

        var aborted = await backend.AbortAsync(Abort(), Now, TestContext.Current.CancellationToken);
        var replay = await backend.AbortAsync(Abort(), Now, TestContext.Current.CancellationToken);

        aborted.ShouldBe(new ArtifactStoreAborted(false));
        replay.ShouldBe(new ArtifactStoreAborted(true));
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 29104);
    }

    [Fact]
    public async Task DeleteAsync_WhenTwoVersionsShareBytes_KeepsThePayloadUntilTheLastReferenceIsGone()
    {
        using var backend = new FaultInjectingArtifactBackend();
        var first = await CommitAsync(backend, 1, 1);
        var second = await CommitAsync(backend, 2, 2);

        _ = await backend.DeleteAsync(Delete(first), Now, TestContext.Current.CancellationToken);
        _ = await backend.DeleteAsync(Delete(second), Now, TestContext.Current.CancellationToken);

        backend.Releases.ShouldBe([true, false]);
    }

    [Fact]
    public async Task ReadAsync_WhenThePayloadIsMissingOrUnreadable_ReportsUnavailableNeverAnEmptyStream()
    {
        using var backend = new FaultInjectingArtifactBackend();
        var reference = await CommitAsync(backend, 1, 1);

        backend.PayloadMissing = true;
        var missing = await backend.ReadAsync(Read(reference), TestContext.Current.CancellationToken);
        backend.PayloadMissing = false;
        backend.OpenFailure = new IOException("disk");
        var failed = await backend.ReadAsync(Read(reference), TestContext.Current.CancellationToken);
        backend.OpenFailure = null;
        var opened = await backend.ReadAsync(Read(reference), TestContext.Current.CancellationToken);

        missing.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        failed.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        await using var stream = opened.ShouldBeOfType<ArtifactStoreReadOpened>();
        stream.Content.Length.ShouldBe(_content.Length);
    }

    [Fact]
    public async Task Operations_WhenAnUnexpectedExceptionIsThrown_PropagatesItWithoutMaskingItAsUnavailable()
    {
        using var backend = new FaultInjectingArtifactBackend { PersistFailure = new InvalidOperationException("bug") };

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Operations_WhenCancelledBeforeTheGateIsHeld_Throw()
    {
        using var backend = new FaultInjectingArtifactBackend();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await backend.PrepareAsync(Prepare(_content), Now, cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await backend.InitializeForTestAsync(cancelled.Token));
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await backend.ReadAsync(Read(await CommitAsync(new FaultInjectingArtifactBackend(), 1, 1)), cancelled.Token));
    }

    [Fact]
    public async Task Dispose_WhenCalledTwice_ReleasesTheGateOnceAndRejectsFurtherUse()
    {
        var backend = new FaultInjectingArtifactBackend();

        backend.Dispose();
        backend.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await backend.PrepareAsync(Prepare(_content), Now, TestContext.Current.CancellationToken));
    }

    private static async Task<ArtifactReference> CommitAsync(FaultInjectingArtifactBackend backend, int preparation, int artifact)
    {
        _ = await backend.PrepareAsync(Prepare(_content, preparation, artifact, key: $"key-{preparation}"), Now, TestContext.Current.CancellationToken);
        return (await backend.FinalizeAsync(Publish(preparation), Now, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreFinalized>().Reference;
    }

    private sealed class BlankAdapterBackend(): ArtifactStateBackend(" ", null)
    {
        protected override ValueTask RecoverAsync(SecurityAuthorizationContext? authorization, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        protected override ValueTask StagePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, ImmutableArray<byte> content, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        protected override ValueTask PersistAsync(SecurityAuthorizationContext authorization, ImmutableArray<ArtifactEntry> upserts, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        protected override ValueTask ReleasePayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, bool stillReferenced, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        protected override ValueTask<byte[]?> OpenPayloadAsync(SecurityAuthorizationContext authorization, ArtifactEntry entry, CancellationToken cancellationToken) => ValueTask.FromResult<byte[]?>(null);
    }
}
