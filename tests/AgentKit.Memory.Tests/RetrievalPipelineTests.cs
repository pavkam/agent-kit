// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies the retrieval pipeline's staging, omission accounting, fail-closed exposure, and isolation.</summary>
public sealed class RetrievalPipelineTests
{
    private static readonly RetrievalSourceKey _scriptedKey = new("tests.scripted");
    private static readonly RetrievalSourceKey _faultyKey = new("tests.faulty");

    internal sealed class ScriptedBehavior
    {
        internal List<Func<RetrievalSourceRequest, RetrievalCandidate>> Candidates { get; } = [];
    }

    internal sealed class ScriptedSource(ScriptedBehavior behavior): IRetrievalSource
    {
        public RetrievalSourceDescriptor Descriptor { get; } = new(_scriptedKey, "1", requiresEmbedding: false, new ComponentId("tests.scripted-source"));

        public ValueTask<RetrievalSourceResult> SearchAsync(RetrievalSourceRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(RetrievalSourceResult.Succeeded([.. behavior.Candidates.Select(factory => factory(request))], 7));
    }

    internal sealed class FaultySource: IRetrievalSource
    {
        public RetrievalSourceDescriptor Descriptor { get; } = new(_faultyKey, "1", requiresEmbedding: false, new ComponentId("tests.faulty-source"));

        public ValueTask<RetrievalSourceResult> SearchAsync(RetrievalSourceRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The source failed.");
    }

    internal sealed class FailingRequiredSink: IMemoryEventSink
    {
        public ValueTask PublishAsync(MemoryEvent memoryEvent, CancellationToken cancellationToken = default) =>
            memoryEvent.Kind == MemoryEventKind.RetrievalCompleted ? throw new InvalidOperationException("The sink failed.") : ValueTask.CompletedTask;
    }

    private static async Task<DurableMemoryRecord> RememberAsync(MemoryHarness harness, MemoryTestOwner owner, string text, DataClassification classification = DataClassification.Internal)
    {
        var result = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner, text, classification), TestContext.Current.CancellationToken);
        return result.Record!;
    }

    [Fact]
    public async Task RetrieveAsync_WhenMemoriesMatch_ReturnsOnlyMatchingActiveRecordsAsUntrustedData()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var match = await RememberAsync(harness, owner, "The user prefers concise answers.");
        _ = await RememberAsync(harness, owner, "The user lives in a coastal town.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.MemoryId.ShouldBe(match.Id);
        candidate.Trust.ShouldBe(TrustClassification.UntrustedData);
        candidate.Provenance.ShouldBe(match.Provenance);
        candidate.Source.Key.ShouldBe(MemoryRetrievalSourceKeys.DurableMemory);
        result.Summary!.Searched.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenARecordWasCorrected_ReturnsOnlyTheReplacement()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var original = await RememberAsync(harness, owner, "The deployment region is westeurope.");
        _ = await harness.Coordinator.CorrectAsync(
            new MemoryCorrectionRequest(
                owner.Context, original.Id, original.Version, new MemoryId(Guid.NewGuid()), new MemoryContent("The deployment region is northeurope."),
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("c-1")),
            TestContext.Current.CancellationToken);

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "deployment region"), TestContext.Current.CancellationToken);

        result.Candidates.Select(static candidate => candidate.Content.Text).ShouldBe(["The deployment region is northeurope."]);
    }

    [Fact]
    public async Task RetrieveAsync_WhenARecordWasDeleted_NeverReturnsIt()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var record = await RememberAsync(harness, owner, "The launch code is swordfish.");
        _ = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, record.Id, record.Version, MemoryDeleteMode.Purge, new IdempotencyKey("d-1")), TestContext.Current.CancellationToken);

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "launch code"), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        result.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenAnotherTenantQueries_ReturnsNothing()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var stranger = MemoryTestData.NewOwner("tenant-b");
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(stranger, "concise answers"), TestContext.Current.CancellationToken);

        result.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenQueryCeilingExceedsTheProfile_FailsBeforeAuthorizing()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.MaximumClassification = DataClassification.Internal);
        var owner = MemoryTestData.NewOwner();

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, maximumClassification: DataClassification.Restricted), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ClassificationExceeded);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenACandidateExceedsTheQueryCeiling_OmitsItAsUnauthorized()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The project codename is aurora.", DataClassification.Confidential);

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "project codename", maximumClassification: DataClassification.Internal), TestContext.Current.CancellationToken);

        result.Candidates.ShouldBeEmpty();
        result.Summary!.OmittedUnauthorized.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenRetrievalIsDisabled_FailsWithRetrievalDisabled()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.EnableRetrieval = false);
        var owner = MemoryTestData.NewOwner();

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.RetrievalDisabled);
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheAuthorityDeniesTheQuery_FailsWithDenied()
    {
        using var harness = MemoryHarness.Create();
        harness.Authority.Deny = true;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.Denied);
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheAuthorityFails_FailsAsProfileUnavailable()
    {
        using var harness = MemoryHarness.Create();
        harness.Authority.Throw = true;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ProfileUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheProfileIsUnknown_FailsAsProfileUnavailable()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var unknown = owner with
        {
            Context = new MemoryOperationContext(
                owner.AgentId, owner.SessionId, owner.Identity, owner.Context.Correlation, owner.Authorization, new MemoryProfileKey("missing"), MemoryTestData.ProfileVersion),
        };

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(unknown), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ProfileUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenACandidateIsStale_OmitsItAndCountsIt()
    {
        var behavior = new ScriptedBehavior();
        using var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddSingleton(behavior);
                _ = services.AddRetrievalSource<ScriptedSource>(_scriptedKey);
            },
            profile: configured => configured.RetrievalSources = [_scriptedKey]);
        var owner = MemoryTestData.NewOwner();
        var live = await RememberAsync(harness, owner, "A live fact about caching.");
        behavior.Candidates.Add(request => new RetrievalCandidate(
            request.Query.Id, new RetrievalSourceIdentity(_scriptedKey, "1", 7), live.Id, null, null, new CandidateContent("A live fact about caching."),
            live.Provenance, TrustClassification.UntrustedData, 0.9, DataClassification.Internal));
        behavior.Candidates.Add(request => new RetrievalCandidate(
            request.Query.Id, new RetrievalSourceIdentity(_scriptedKey, "1", 7), new MemoryId(Guid.NewGuid()), null, null, new CandidateContent("A fact that no longer exists."),
            new Provenance("index"), TrustClassification.UntrustedData, 0.8, DataClassification.Internal));
        behavior.Candidates.Add(request => new RetrievalCandidate(
            request.Query.Id, new RetrievalSourceIdentity(_scriptedKey, "1", 7), live.Id, null, null, new CandidateContent("Changed text since indexing."),
            live.Provenance, TrustClassification.UntrustedData, 0.7, DataClassification.Internal));

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner), TestContext.Current.CancellationToken);

        result.Candidates.Select(static candidate => candidate.Content.Text).ShouldBe(["A live fact about caching."]);
        result.Summary!.OmittedStale.ShouldBe(2);
        result.Summary.DeletionGeneration.ShouldBe(7);
    }

    [Fact]
    public async Task RetrieveAsync_WhenCandidatesDuplicateByIdentityOrText_KeepsTheBestOnce()
    {
        var behavior = new ScriptedBehavior();
        using var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddSingleton(behavior);
                _ = services.AddRetrievalSource<ScriptedSource>(_scriptedKey);
            },
            profile: configured => configured.RetrievalSources = [_scriptedKey, MemoryRetrievalSourceKeys.DurableMemory]);
        var owner = MemoryTestData.NewOwner();
        var live = await RememberAsync(harness, owner, "Caching uses a sliding expiry.");
        behavior.Candidates.Add(request => new RetrievalCandidate(
            request.Query.Id, new RetrievalSourceIdentity(_scriptedKey, "1", 7), live.Id, null, null, new CandidateContent("Caching uses a sliding expiry."),
            live.Provenance, TrustClassification.UntrustedData, 0.1, DataClassification.Internal));

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "caching sliding expiry"), TestContext.Current.CancellationToken);

        _ = result.Candidates.ShouldHaveSingleItem();
        result.Summary!.OmittedDuplicate.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenOneOfTwoSourcesFaults_CompletesAndCountsTheUnavailableSource()
    {
        using var harness = MemoryHarness.Create(
            arrange: services => services.AddRetrievalSource<FaultySource>(_faultyKey),
            profile: configured => configured.RetrievalSources = [MemoryRetrievalSourceKeys.DurableMemory, _faultyKey]);
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        _ = result.Candidates.ShouldHaveSingleItem();
        result.Summary!.SourcesUnavailable.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenEverySourceFails_FailsWithSourcesUnavailable()
    {
        using var harness = MemoryHarness.Create(
            arrange: services => services.AddRetrievalSource<FaultySource>(_faultyKey),
            profile: configured => configured.RetrievalSources = [_faultyKey]);

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.SourcesUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenExposureIsDeniedPerCandidate_ExposesNothingAndCountsIt()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");
        harness.Authority.DenyEffect = SecurityEffect.Egress;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        result.Candidates.ShouldBeEmpty();
        result.Summary!.OmittedUnauthorized.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenExposureAuthorizationCannotBeEvaluated_FailsClosed()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");
        harness.Authority.ThrowEffect = SecurityEffect.Egress;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ExposureUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenExposureAuthorizationIsNotRequired_SkipsTheEgressCheck()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.RequireExposureAuthorization = false, options: engine => engine.RequireExposureAuthorization = false);
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");
        harness.Authority.DenyEffect = SecurityEffect.Egress;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), TestContext.Current.CancellationToken);

        _ = result.Candidates.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RetrieveAsync_WhenARequiredSinkCannotRecordTheRetrieval_ExposesNothing()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
            services.AddMemoryEventSink<FailingRequiredSink>(new MemoryEventSinkRegistration(new ComponentId("tests.failing"), 0, MemoryEventDelivery.Required, ServiceLifetime.Singleton)));
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ExposureUnavailable);
        result.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheBudgetIsSmallerThanTheResults_OmitsTheOverflow()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "Caching note one about expiry.");
        _ = await RememberAsync(harness, owner, "Caching note two about expiry.");
        _ = await RememberAsync(harness, owner, "Caching note three about expiry.");

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "caching expiry", new RetrievalBudget(10, 70, 2_000)), TestContext.Current.CancellationToken);

        result.Candidates.Length.ShouldBe(2);
        result.Summary!.OmittedByBudget.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenRewritingIsEnabled_CompletesThroughTheCapturedRewriter()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.EnableQueryRewriting = true);
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), TestContext.Current.CancellationToken);

        _ = result.Candidates.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RetrieveAsync_WhenQueryIsNull_ThrowsArgumentNullException()
    {
        using var harness = MemoryHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await harness.Pipeline.RetrieveAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("query");
    }

    [Fact]
    public async Task RetrieveAsync_WhenAlreadyCancelled_PropagatesCancellation()
    {
        using var harness = MemoryHarness.Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), source.Token));
    }
}
