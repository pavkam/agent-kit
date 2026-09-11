// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolDiscoveryCaptureTests
{
    [Fact]
    public void Constructor_WhenInputsInvalid_RejectsBeforeMetadataOrCleanup()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var source = new CallbackToolProviderCapture(candidate.Source);
        var selection = Selection(candidate.Toolset, [candidate.Source]);
        var snapshots = ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(candidate.Source.SourceId, candidate.Source);
        var sources = ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(candidate.Source.SourceId, source);
        var clock = TimeProvider.System;
        var logger = NullLogger<ToolDiscoveryCapture>.Instance;
        var catalogLogger = NullLogger<ToolCatalogCapture>.Instance;
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(null!, snapshots, sources, clock, logger, catalogLogger), "selection");
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(selection, null!, sources, clock, logger, catalogLogger), "sourceSnapshots");
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, null!, clock, logger, catalogLogger), "sources");
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, sources, null!, logger, catalogLogger), "timeProvider");
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, sources, clock, null!, catalogLogger), "logger");
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, sources, clock, logger, null!), "catalogLogger");
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(selection, snapshots.SetItem(candidate.Source.SourceId, null!), sources, clock, logger, catalogLogger), "sourceSnapshots");
        Exact<ArgumentNullException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, sources.SetItem(candidate.Source.SourceId, null!), clock, logger, catalogLogger), "sources");
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolDiscoveryCapture(selection, snapshots.Add(default, candidate.Source), sources, clock, logger, catalogLogger), "sourceSnapshots");
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, sources.Add(default, source), clock, logger, catalogLogger), "sources");
        Exact<ArgumentException>(() => _ = new ToolDiscoveryCapture(selection, snapshots.Add(new("wrong"), candidate.Source), sources, clock, logger, catalogLogger), "sourceSnapshots");
        Exact<ArgumentException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, sources.Add(new("other"), source), clock, logger, catalogLogger), "sources");
        Exact<ArgumentException>(() => _ = new ToolDiscoveryCapture(selection, [], sources, clock, logger, catalogLogger), "sourceSnapshots");
        Exact<ArgumentException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, [], clock, logger, catalogLogger), "sources");
        var other = ToolCatalogMergeTestData.Source("other", []);
        Exact<ArgumentException>(() => _ = new ToolDiscoveryCapture(selection, ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(other.SourceId, other), sources, clock, logger, catalogLogger), "sourceSnapshots");
        Exact<ArgumentException>(() => _ = new ToolDiscoveryCapture(selection, snapshots, ImmutableDictionary<ToolSourceId, IToolProviderCapture>.Empty.Add(other.SourceId, source), clock, logger, catalogLogger), "sources");
        source.SnapshotReads.ShouldBe(0);
        source.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task TransferToCatalog_WhenValid_UsesRetainedMetadataAndTransfersExactInvokerOwnership()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var invoker = new CaptureTestToolInvoker();
        var leaseReleases = 0;
        var source = new CallbackToolProviderCapture(candidate.Source)
        {
            Acquire = (_, _) => ValueTask.FromResult<ToolInvokerLeaseResult>(new ToolInvokerAcquired(new ToolInvokerLease(candidate.Tool, candidate.Source.SourceVersion, invoker,
                () => { leaseReleases++; return ValueTask.CompletedTask; }))),
            ReadSnapshot = static () => throw new InvalidOperationException("live metadata must not be consulted"),
        };
        var owner = Create(candidate.Toolset, [(candidate.Source, source)]);
        var snapshot = await MergeAsync(owner);
        var catalog = owner.TransferToCatalog(snapshot, TestContext.Current.CancellationToken);
        await owner.DisposeAsync();
        source.Disposals.ShouldBe(0);
        source.SnapshotReads.ShouldBe(0);
        await using var lease = (await catalog.AcquireInvokerAsync(candidate.Identity, TestContext.Current.CancellationToken)).ShouldBeOfType<ToolInvokerAcquired>().Lease;
        lease.Invoker.ShouldBeSameAs(invoker);
        var closing = catalog.DisposeAsync().AsTask();
        closing.IsCompleted.ShouldBeFalse();
        await lease.DisposeAsync();
        await closing;
        await owner.DisposeAsync();
        source.Disposals.ShouldBe(1);
        leaseReleases.ShouldBe(1);
        invoker.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task TransferToCatalog_WhenCancelledOrInvalid_RetainsSourcesForLaterValidTransfer()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var source = new CallbackToolProviderCapture(candidate.Source);
        var owner = Create(candidate.Toolset, [(candidate.Source, source)]);
        var snapshot = await MergeAsync(owner);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Exact<ArgumentNullException>(() => owner.TransferToCatalog(null!, cancellation.Token), "snapshot");
        Should.Throw<OperationCanceledException>(() => owner.TransferToCatalog(snapshot, cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token);
        var otherRequest = ToolCatalogMergeTestData.Request([candidate.Toolset], "other");
        var foreign = await MergeAsync(owner, otherRequest);
        Exact<ArgumentException>(() => owner.TransferToCatalog(foreign, TestContext.Current.CancellationToken), "snapshot");
        var changedSource = ToolCatalogMergeTestData.Source(candidate.Source.SourceId.Value, candidate.Source.Tools, "changed");
        var changedOwner = Create(candidate.Toolset, [(changedSource, new CallbackToolProviderCapture(changedSource))]);
        await using var changedLifetime = changedOwner;
        var changed = await MergeAsync(changedOwner);
        _ = Should.Throw<ArgumentException>(() => owner.TransferToCatalog(changed, TestContext.Current.CancellationToken));
        source.Disposals.ShouldBe(0);
        source.SnapshotReads.ShouldBe(0);
        await using var catalog = owner.TransferToCatalog(snapshot, TestContext.Current.CancellationToken);
        _ = Should.Throw<InvalidOperationException>(() => owner.TransferToCatalog(snapshot, TestContext.Current.CancellationToken));
        await owner.DisposeAsync();
        source.Disposals.ShouldBe(0);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("session")]
    [InlineData("run")]
    [InlineData("identity")]
    [InlineData("security-policy")]
    [InlineData("definition")]
    [InlineData("configuration")]
    public async Task TransferToCatalog_WhenRequestEvidenceDiffers_RejectsWithoutLosingOwnership(string field)
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var source = new CallbackToolProviderCapture(candidate.Source);
        var owner = Create(candidate.Toolset, [(candidate.Source, source)]);
        var snapshot = await MergeAsync(owner);
        var otherId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var foreign = new ToolCatalogSnapshot(
            field == "agent" ? new AgentId(otherId) : snapshot.AgentId,
            field == "session" ? new SessionId(otherId) : snapshot.SessionId,
            field == "run" ? new RunId(otherId) : snapshot.RunId,
            field == "identity" ? TestExecutionIdentity.Create(new TenantId("other"), new PrincipalId("principal"), ExecutionSubjectKind.Human) : snapshot.Identity,
            field == "security-policy" ? new SecurityPolicySnapshotReference(new SecurityPolicySnapshotId(otherId), new SecurityPolicyVersion(2), new ContentHash("sha256:other")) : snapshot.SecurityPolicy,
            field == "definition" ? new AgentDefinitionRevision(2) : snapshot.AgentDefinitionRevision,
            field == "configuration" ? new ConfigurationVersion(2) : snapshot.ConfigurationVersion,
            snapshot.Version, snapshot.SourceVersions, snapshot.Tools, snapshot.ExecutionPolicies, snapshot.ProviderAliases);
        Exact<ArgumentException>(() => owner.TransferToCatalog(foreign, TestContext.Current.CancellationToken), "snapshot");
        source.Disposals.ShouldBe(0);
        source.SnapshotReads.ShouldBe(0);
        await using var catalog = owner.TransferToCatalog(snapshot, TestContext.Current.CancellationToken);
        await owner.DisposeAsync();
        source.Disposals.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_WhenCleanupsPending_StartsEveryReleaseAndSharesOrderedFailure()
    {
        var z = ToolCatalogMergeTestData.Source("z", []);
        var a = ToolCatalogMergeTestData.Source("a", []);
        var zCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var zSource = new CallbackToolProviderCapture(z) { Release = () => new(zCompletion.Task) };
        var aSource = new CallbackToolProviderCapture(a) { Release = () => new(aCompletion.Task) };
        var owner = Create(ToolCatalogMergeTestData.Toolset("tools", [z, a], []), [(z, zSource), (a, aSource)]);
        var snapshot = await MergeAsync(owner);
        var first = owner.DisposeAsync().AsTask();
        var second = owner.DisposeAsync().AsTask();
        zSource.Disposals.ShouldBe(1);
        aSource.Disposals.ShouldBe(1);
        _ = Should.Throw<InvalidOperationException>(() => owner.TransferToCatalog(snapshot, TestContext.Current.CancellationToken));
        first.IsCompleted.ShouldBeFalse();
        var zFailure = new InvalidOperationException("z");
        var aFailure = new InvalidOperationException("a");
        zCompletion.SetException(zFailure);
        aCompletion.SetException(aFailure);
        var failure = await Should.ThrowAsync<AggregateException>(() => first);
        failure.InnerExceptions.ShouldBe([aFailure, zFailure]);
        (await Should.ThrowAsync<AggregateException>(() => second)).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<AggregateException>(async () => await owner.DisposeAsync())).ShouldBeSameAs(failure);
        zSource.Disposals.ShouldBe(1);
        aSource.Disposals.ShouldBe(1);
        owner.SourceSnapshots.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DisposeAsync_WhenSingleCleanupFails_PreservesOriginalFailureWithoutRetry()
    {
        var snapshot = ToolCatalogMergeTestData.Source("source", []);
        var failure = new InvalidOperationException("source cleanup");
        var source = new CallbackToolProviderCapture(snapshot) { Release = () => throw failure };
        var owner = Create(ToolCatalogMergeTestData.Toolset("tools", [snapshot], []), [(snapshot, source)]);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await owner.DisposeAsync())).ShouldBeSameAs(failure);
        (await Should.ThrowAsync<InvalidOperationException>(async () => await owner.DisposeAsync())).ShouldBeSameAs(failure);
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task TransferToCatalog_WhenRacingClosure_HasOneOwnerAndReleasesSourcesOnce()
    {
        for (var index = 0; index < 32; index++)
        {
            var snapshot = ToolCatalogMergeTestData.Source("source", []);
            var source = new CallbackToolProviderCapture(snapshot);
            var owner = Create(ToolCatalogMergeTestData.Toolset("tools", [snapshot], []), [(snapshot, source)]);
            var merged = await MergeAsync(owner);
            ToolCatalogCapture? transferred = null;
            await Task.WhenAll(
                Task.Run(() =>
                {
                    try { transferred = owner.TransferToCatalog(merged, TestContext.Current.CancellationToken); }
                    catch (InvalidOperationException) { }
                }, TestContext.Current.CancellationToken),
                Task.Run(async () => await owner.DisposeAsync(), TestContext.Current.CancellationToken));
            if (transferred is not null)
            {
                source.Disposals.ShouldBe(0);
                await transferred.DisposeAsync();
            }
            source.Disposals.ShouldBe(1);
            source.SnapshotReads.ShouldBe(0);
        }
    }

    [Fact]
    public async Task ReleaseSourcesAsync_WhenArgumentsInvalid_RejectsBeforeAnyCleanupOrObservation()
    {
        var request = ToolCaptureTestData.Discovery();
        var snapshot = ToolCatalogMergeTestData.Source("source", []);
        var source = new CallbackToolProviderCapture(snapshot);
        var clockReads = 0;
        var clock = new CallbackTimestampTimeProvider(() => ++clockReads);
        var logger = new RecordingLogger<ToolDiscoveryCapture>();
        ImmutableArray<KeyValuePair<ToolSourceId, IToolProviderCapture>> sources = [new(snapshot.SourceId, source)];
        await ThrowsAsync<ArgumentNullException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(null!, sources, clock, logger), "request");
        await ThrowsAsync<ArgumentException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(request, default, clock, logger), "sources");
        await ThrowsAsync<ArgumentNullException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(request, sources, null!, logger), "timeProvider");
        await ThrowsAsync<ArgumentNullException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(request, sources, clock, null!), "logger");
        await ThrowsAsync<ArgumentOutOfRangeException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(request, [new(default, source)], clock, logger), "sources");
        await ThrowsAsync<ArgumentNullException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(request, [new(snapshot.SourceId, null!)], clock, logger), "sources");
        await ThrowsAsync<ArgumentException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(request, [sources[0], sources[0]], clock, logger), "sources");
        await ThrowsAsync<ArgumentException>(() => ToolDiscoveryCapture.ReleaseSourcesAsync(request, [sources[0], new(new ToolSourceId("other"), source)], clock, logger), "sources");
        source.Disposals.ShouldBe(0);
        source.SnapshotReads.ShouldBe(0);
        clockReads.ShouldBe(0);
        logger.Snapshot().ShouldBeEmpty();
    }

    private static ToolDiscoveryCapture Create(ToolsetPublication toolset, ImmutableArray<(ToolProviderSnapshot Snapshot, CallbackToolProviderCapture Capture)> sources) =>
        new(Selection(toolset, [.. sources.Select(static source => source.Snapshot)]), sources.ToImmutableDictionary(static source => source.Snapshot.SourceId, static source => source.Snapshot),
            sources.ToImmutableDictionary(static source => source.Snapshot.SourceId, static source => (IToolProviderCapture) source.Capture), TimeProvider.System,
            NullLogger<ToolDiscoveryCapture>.Instance, NullLogger<ToolCatalogCapture>.Instance);

    private static ToolDiscoverySelection Selection(ToolsetPublication toolset, ImmutableArray<ToolProviderSnapshot> sources) => new(ToolCatalogMergeTestData.Request([toolset]), [toolset],
        [.. sources.Select(static source => new ToolProviderBinding(source.SourceId, new CallbackToolProvider(source.SourceId)))]);

    private static async ValueTask<ToolCatalogSnapshot> MergeAsync(ToolDiscoveryCapture capture, ToolDiscoveryRequest? request = null)
    {
        var merger = new ToolCatalogMerger(new RejectingToolCatalogMergePolicy(TimeProvider.System, NullLogger<RejectingToolCatalogMergePolicy>.Instance), TimeProvider.System, NullLogger<ToolCatalogMerger>.Instance);
        var (snapshot, _) = await merger.MergeAsync(request ?? capture.Selection.Request, new ToolCatalogVersion("catalog"), capture.Selection.Toolsets, capture.SourceSnapshots, TestContext.Current.CancellationToken);
        return snapshot.ShouldNotBeNull();
    }

    private static async Task ThrowsAsync<TException>(Func<Task<ImmutableArray<Exception>>> action, string parameter) where TException : ArgumentException
    {
        var error = await Should.ThrowAsync<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var error = Should.Throw<TException>(action);
        error.GetType().ShouldBe(typeof(TException));
        error.ParamName.ShouldBe(parameter);
    }
}
