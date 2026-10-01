// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

using AgentKit.Hooks;

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
        var result = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner, text, classification), hooks: null, TestContext.Current.CancellationToken);
        return result.Record!;
    }

    [Fact]
    public async Task RetrieveAsync_WhenMemoriesMatch_ReturnsOnlyMatchingActiveRecordsAsUntrustedData()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var match = await RememberAsync(harness, owner, "The user prefers concise answers.");
        _ = await RememberAsync(harness, owner, "The user lives in a coastal town.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        var candidate = result.Candidates.ShouldHaveSingleItem();
        candidate.MemoryId.ShouldBe(match.Id);
        candidate.Trust.ShouldBe(TrustClassification.UntrustedData);
        candidate.Provenance.ShouldBe(match.Provenance);
        candidate.Source.Key.ShouldBe(MemoryRetrievalSourceKeys.DurableMemory);
        result.Summary!.Searched.ShouldBe(1);
    }

    private sealed class ScriptedRetrievalHook(string name, List<string> log, Action<BeforeRetrievalEventArgs>? act = null): IBeforeRetrievalHook
    {
        public ValueTask InvokeAsync(BeforeRetrievalEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default)
        {
            log.Add(name);
            act?.Invoke(args);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptedExposureHook(string name, List<string> log, Action<BeforeRetrievalExposureEventArgs>? act = null): IBeforeRetrievalExposureHook
    {
        public ValueTask InvokeAsync(BeforeRetrievalExposureEventArgs args, HookInvocationContext context, CancellationToken cancellationToken = default)
        {
            log.Add(name);
            act?.Invoke(args);
            return ValueTask.CompletedTask;
        }
    }

    private static MemoryHarness WithHooks() => MemoryHarness.Create(arrange: services => _ = services.AddAgentHooks());

    private static async Task SeedAsync(MemoryHarness harness, MemoryTestOwner owner, int count)
    {
        for (var index = 0; index < count; index++)
        {
            _ = await RememberAsync(harness, owner, $"The user prefers concise answers number {index}.");
        }
    }

    [Fact]
    public async Task RetrieveAsync_WhenARetrievalHookNarrowsTheBudget_ReturnsNoMoreThanTheNarrowedItems()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 3);
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("narrow", AgentHookPointDefinitions.BeforeRetrievalRegistration), new ScriptedRetrievalHook("narrow", [], args => args.MaximumItems = 1)));

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "concise answers"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        _ = result.Candidates.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("items")]
    [InlineData("bytes")]
    [InlineData("zero")]
    public async Task RetrieveAsync_WhenARetrievalHookWidensOrZeroesTheBudget_FailsClosedWithoutSearching(string mutation)
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 2);
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("widen", AgentHookPointDefinitions.BeforeRetrievalRegistration), new ScriptedRetrievalHook("widen", [], args =>
            {
                switch (mutation)
                {
                    case "items":
                        args.MaximumItems = args.EffectiveBudget.MaximumItems + 1;
                        break;
                    case "bytes":
                        args.MaximumBytes = args.EffectiveBudget.MaximumBytes + 1;
                        break;
                    default:
                        args.MaximumItems = 0;
                        break;
                }
            })));
        harness.Authority.Requests.Clear();

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "concise answers"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeFalse();
        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.Denied);
        result.Failure!.SafeMessage.ShouldBe("A memory hook failed, so the operation was refused.");
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenTwoRetrievalHooksRun_TheLaterSeesTheEarlierNarrowingInCatalogOrder()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 3);
        var log = new List<string>();
        var seenByLast = 0;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("last", AgentHookPointDefinitions.BeforeRetrievalRegistration, HookOrder.Last), new ScriptedRetrievalHook("last", log, args => seenByLast = args.MaximumItems)),
            (MemoryHookScope.Register("first", AgentHookPointDefinitions.BeforeRetrievalRegistration, HookOrder.First), new ScriptedRetrievalHook("first", log, args => args.MaximumItems = 2)));

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "concise answers"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        log.ShouldBe(["first", "last"]);
        seenByLast.ShouldBe(2);
        result.Candidates.Length.ShouldBe(2);
    }

    [Fact]
    public async Task RetrieveAsync_WhenAnExposureHookExcludesCandidates_DropsThemAndCountsThemUnauthorized()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 3);
        ImmutableArray<RetrievalCandidate> offered = default;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("drop", AgentHookPointDefinitions.BeforeRetrievalExposureRegistration), new ScriptedExposureHook("drop", [], args =>
            {
                offered = args.Candidates;
                args.ExcludedPositions = [0, 2];
            })));

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "concise answers"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        offered.Length.ShouldBe(3);
        result.Candidates.ShouldBe([offered[1]]);
        result.Summary!.OmittedUnauthorized.ShouldBe(2);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task RetrieveAsync_WhenAnExposureHookExcludesAPositionOutsideTheOfferedSet_ExposesNothing(int position)
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 3);
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("bad", AgentHookPointDefinitions.BeforeRetrievalExposureRegistration), new ScriptedExposureHook("bad", [], args => args.ExcludedPositions = [position])));

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "concise answers"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeFalse();
        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ExposureUnavailable);
        result.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenAnExposureHookDuplicatesAPosition_ExposesNothing()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 2);
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("dup", AgentHookPointDefinitions.BeforeRetrievalExposureRegistration), new ScriptedExposureHook("dup", [], args => args.ExcludedPositions = [0, 0])));

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "concise answers"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ExposureUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenAnExposureHookThrows_ExposesNothingAndLeaksNoDetail()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 2);
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("boom", AgentHookPointDefinitions.BeforeRetrievalExposureRegistration), new ScriptedExposureHook(
                "boom", [], _ => throw new InvalidOperationException("secret detail"))));

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "concise answers"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ExposureUnavailable);
        result.Failure!.SafeMessage.ShouldBe("A memory hook failed, so the operation was refused.");
    }

    [Fact]
    public async Task RetrieveAsync_WhenNothingSurvivesToExposure_DispatchesNoExposureHook()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        var log = new List<string>();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("drop", AgentHookPointDefinitions.BeforeRetrievalExposureRegistration), new ScriptedExposureHook("drop", log)));

        var result = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(owner, "nothing stored"), MemoryHookScope.Context(scope, harness, owner), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        log.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenNoHookContextIsSupplied_RunsNoRegisteredHook()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 2);
        var log = new List<string>();
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("retrieval", AgentHookPointDefinitions.BeforeRetrievalRegistration), new ScriptedRetrievalHook("retrieval", log)),
            (MemoryHookScope.Register("exposure", AgentHookPointDefinitions.BeforeRetrievalExposureRegistration), new ScriptedExposureHook("exposure", log)));

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        log.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenHooksRun_EachSeesItsOwnPointAndTheEffectiveQuery()
    {
        using var harness = WithHooks();
        var owner = MemoryTestData.NewOwner();
        await SeedAsync(harness, owner, 1);
        BeforeRetrievalEventArgs? retrievalArgs = null;
        BeforeRetrievalExposureEventArgs? exposureArgs = null;
        await using var scope = await MemoryHookScope.OpenAsync(
            (MemoryHookScope.Register("retrieval", AgentHookPointDefinitions.BeforeRetrievalRegistration), new ScriptedRetrievalHook("retrieval", [], args => retrievalArgs = args)),
            (MemoryHookScope.Register("exposure", AgentHookPointDefinitions.BeforeRetrievalExposureRegistration), new ScriptedExposureHook("exposure", [], args => exposureArgs = args)));
        var context = MemoryHookScope.Context(scope, harness, owner);
        var query = MemoryTestData.Query(owner, "concise answers");

        _ = await harness.Pipeline.RetrieveAsync(query, context, TestContext.Current.CancellationToken);

        retrievalArgs!.Point.ShouldBe(AgentHookPoints.BeforeRetrieval);
        exposureArgs!.Point.ShouldBe(AgentHookPoints.BeforeRetrievalExposure);
        retrievalArgs.Query.Id.ShouldBe(query.Id);
        exposureArgs.Query.Id.ShouldBe(query.Id);
        retrievalArgs.Correlation.ShouldBe(context.Dispatch.Correlation);
        retrievalArgs.AgentId.ShouldBe(owner.Context.AgentId);
        exposureArgs.SessionId.ShouldBe(owner.Context.SessionId);
    }

    internal sealed class EmbeddingRequiringSource: IRetrievalSource
    {
        public RetrievalSourceDescriptor Descriptor { get; } = new(new RetrievalSourceKey("tests.embedding"), "1", requiresEmbedding: true, new ComponentId("tests.embedding-source"));

        public ValueTask<RetrievalSourceResult> SearchAsync(RetrievalSourceRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(RetrievalSourceResult.Succeeded([], 0));
    }

    internal sealed class NoRerankerSelector: IRerankerSelector
    {
        public ValueTask<RerankerSelectionResult> SelectAsync(RerankerSelectionRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<RerankerSelectionResult>(new NoCompatibleReranker(RerankerRequirements.None, []));
    }

    internal sealed class UnusedRerankExecutor: IRerankRequestExecutor
    {
        public Task<RerankExecutionResult> ExecuteAsync(RerankExecutionRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The selector never selects a reranker.");
    }

    internal sealed class ThrowingBudgetPolicy: IRetrievalBudgetPolicy
    {
        public ValueTask<RetrievalBudgetDecision> SelectAsync(RetrievalBudgetRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("secret detail");
    }

    private static ComponentKey<IRerankerSelector> RerankerSelectorKey { get; } = new("tests.reranker-selector");

    private static ComponentKey<IRerankRequestExecutor> RerankerExecutorKey { get; } = new("tests.reranker-executor");

    [Fact]
    public async Task RetrieveAsync_WhenCompleted_LogsTheCompletedEventWithCountsOnly()
    {
        var logger = new RecordingLogger<RetrievalPipeline>();
        using var harness = MemoryHarness.Create(arrange: services => services.AddSingleton<ILogger<RetrievalPipeline>>(logger));
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        _ = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32320);
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldNotContain("concise answers");
    }

    [Fact]
    public async Task RetrieveAsync_WhenRefused_LogsTheRejectedEventWithTheBoundedKind()
    {
        var logger = new RecordingLogger<RetrievalPipeline>();
        using var harness = MemoryHarness.Create(
            arrange: services => services.AddSingleton<ILogger<RetrievalPipeline>>(logger),
            profile: configured => configured.MaximumClassification = DataClassification.Internal);

        _ = await harness.Pipeline.RetrieveAsync(
            MemoryTestData.Query(MemoryTestData.NewOwner(), "secret query words", maximumClassification: DataClassification.Restricted), hooks: null, TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32321);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("classification_exceeded");
        entry.Message.ShouldNotContain("secret query words");
    }

    [Fact]
    public async Task RetrieveAsync_WhenASourceFaults_LogsTheSourceUnavailableEventWithTheSourceAndReason()
    {
        var logger = new RecordingLogger<RetrievalPipeline>();
        using var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddSingleton<ILogger<RetrievalPipeline>>(logger);
                _ = services.AddRetrievalSource<FaultySource>(_faultyKey);
            },
            profile: configured => configured.RetrievalSources = [MemoryRetrievalSourceKeys.DurableMemory, _faultyKey]);
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        _ = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().Single(static candidate => candidate.EventId.Id == 32322);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("tests.faulty");
        entry.Message.ShouldContain("faulted");
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheBudgetPolicyFaults_LogsTheFaultedEventWithTheErrorTypeAndRethrows()
    {
        var logger = new RecordingLogger<RetrievalPipeline>();
        using var harness = MemoryHarness.Create(arrange: services =>
        {
            _ = services.AddSingleton<ILogger<RetrievalPipeline>>(logger);
            _ = services.Replace(ServiceDescriptor.Singleton<IRetrievalBudgetPolicy, ThrowingBudgetPolicy>());
        });
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken));

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32323);
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Message.ShouldContain(nameof(InvalidOperationException));
        entry.Message.ShouldNotContain("secret detail");
    }

    [Fact]
    public async Task RetrieveAsync_WhenCancelled_LogsTheCancelledEventAtInformation()
    {
        var logger = new RecordingLogger<RetrievalPipeline>();
        using var harness = MemoryHarness.Create(arrange: services => services.AddSingleton<ILogger<RetrievalPipeline>>(logger));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), hooks: null, source.Token));

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32324);
        entry.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task RetrieveAsync_WhenNoRerankerIsCompatible_KeepsTheRankingAndLogsTheRerankDegradedEvent()
    {
        var logger = new RecordingLogger<RetrievalPipeline>();
        using var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddSingleton<ILogger<RetrievalPipeline>>(logger);
                _ = services.AddMemoryRerankerSelector<NoRerankerSelector>(RerankerSelectorKey);
                _ = services.AddMemoryRerankerExecutor<UnusedRerankExecutor>(RerankerExecutorKey);
                _ = services.AddSingleton<IModelCatalog>(new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])));
            },
            profile: configured =>
            {
                configured.RerankerSelectorKey = RerankerSelectorKey;
                configured.RerankerExecutorKey = RerankerExecutorKey;
                configured.Rerankers = [new RerankerAlias("rank")];
            });
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers number one.");
        _ = await RememberAsync(harness, owner, "The user prefers concise answers number two.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        result.Candidates.Length.ShouldBe(2);
        var entry = logger.Snapshot().Single(static candidate => candidate.EventId.Id == 32325);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("no_compatible_reranker");
    }

    [Fact]
    public async Task RetrieveAsync_WhenASourceNeedsAnEmbeddingTheProfileLacks_LogsTheEmbeddingUnavailableEvent()
    {
        var logger = new RecordingLogger<RetrievalPipeline>();
        var key = new RetrievalSourceKey("tests.embedding");
        using var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddSingleton<ILogger<RetrievalPipeline>>(logger);
                _ = services.AddRetrievalSource<EmbeddingRequiringSource>(key);
            },
            profile: configured => configured.RetrievalSources = [key]);

        _ = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().Single(static candidate => candidate.EventId.Id == 32326);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain("not_configured");
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
                new Provenance("user", owner.RunId, owner.SessionId), new IdempotencyKey("c-1")), hooks: null,
            TestContext.Current.CancellationToken);

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "deployment region"), hooks: null, TestContext.Current.CancellationToken);

        result.Candidates.Select(static candidate => candidate.Content.Text).ShouldBe(["The deployment region is northeurope."]);
    }

    [Fact]
    public async Task RetrieveAsync_WhenARecordWasDeleted_NeverReturnsIt()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var record = await RememberAsync(harness, owner, "The launch code is swordfish.");
        _ = await harness.Coordinator.DeleteAsync(
            new MemoryDeleteCommand(owner.Context, record.Id, record.Version, MemoryDeleteMode.Purge, new IdempotencyKey("d-1")), hooks: null, TestContext.Current.CancellationToken);

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "launch code"), hooks: null, TestContext.Current.CancellationToken);

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

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(stranger, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        result.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenQueryCeilingExceedsTheProfile_FailsBeforeAuthorizing()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.MaximumClassification = DataClassification.Internal);
        var owner = MemoryTestData.NewOwner();

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, maximumClassification: DataClassification.Restricted), hooks: null, TestContext.Current.CancellationToken);

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
            MemoryTestData.Query(owner, "project codename", maximumClassification: DataClassification.Internal), hooks: null, TestContext.Current.CancellationToken);

        result.Candidates.ShouldBeEmpty();
        result.Summary!.OmittedUnauthorized.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenRetrievalIsDisabled_FailsWithRetrievalDisabled()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.EnableRetrieval = false);
        var owner = MemoryTestData.NewOwner();

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner), hooks: null, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.RetrievalDisabled);
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheAuthorityDeniesTheQuery_FailsWithDenied()
    {
        using var harness = MemoryHarness.Create();
        harness.Authority.Deny = true;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.Denied);
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheAuthorityFails_FailsAsProfileUnavailable()
    {
        using var harness = MemoryHarness.Create();
        harness.Authority.Throw = true;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken);

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

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(unknown), hooks: null, TestContext.Current.CancellationToken);

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

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner), hooks: null, TestContext.Current.CancellationToken);

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

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "caching sliding expiry"), hooks: null, TestContext.Current.CancellationToken);

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

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

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

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), hooks: null, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.SourcesUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenExposureIsDeniedPerCandidate_ExposesNothingAndCountsIt()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");
        harness.Authority.DenyEffect = SecurityEffect.Egress;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

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

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.ExposureUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenExposureAuthorizationIsNotRequired_SkipsTheEgressCheck()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.RequireExposureAuthorization = false, options: engine => engine.RequireExposureAuthorization = false);
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");
        harness.Authority.DenyEffect = SecurityEffect.Egress;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        _ = result.Candidates.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RetrieveAsync_WhenARequiredSinkCannotRecordTheRetrieval_ExposesNothing()
    {
        using var harness = MemoryHarness.Create(arrange: services =>
            services.AddMemoryEventSink<FailingRequiredSink>(new MemoryEventSinkRegistration(new ComponentId("tests.failing"), 0, MemoryEventDelivery.Required, ServiceLifetime.Singleton)));
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

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
            MemoryTestData.Query(owner, "caching expiry", new RetrievalBudget(10, 70, 2_000)), hooks: null, TestContext.Current.CancellationToken);

        result.Candidates.Length.ShouldBe(2);
        result.Summary!.OmittedByBudget.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenRewritingIsEnabled_CompletesThroughTheCapturedRewriter()
    {
        using var harness = MemoryHarness.Create(profile: configured => configured.EnableQueryRewriting = true);
        var owner = MemoryTestData.NewOwner();
        _ = await RememberAsync(harness, owner, "The user prefers concise answers.");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "concise answers"), hooks: null, TestContext.Current.CancellationToken);

        _ = result.Candidates.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RetrieveAsync_WhenQueryIsNull_ThrowsArgumentNullException()
    {
        using var harness = MemoryHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await harness.Pipeline.RetrieveAsync(null!, hooks: null, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("query");
    }

    [Fact]
    public async Task RetrieveAsync_WhenAlreadyCancelled_PropagatesCancellation()
    {
        using var harness = MemoryHarness.Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner()), hooks: null, source.Token));
    }
}
