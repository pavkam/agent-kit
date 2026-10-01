// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Retrieval.Tests;

using System.Text.Json;

/// <summary>Verifies the contributor's query construction, candidate projection, and fail-quiet behavior.</summary>
public sealed class RetrievalContextContributorTests
{
    private sealed class ScriptedPipeline(Func<RetrievalQuery, RetrievalResult> script): IRetrievalPipeline
    {
        internal List<RetrievalQuery> Queries { get; } = [];

        public Task<RetrievalResult> RetrieveAsync(RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            return Task.FromResult(script(query));
        }
    }

    private sealed class FixedCatalog(MemoryProfileSnapshot? snapshot): IMemoryProfileCatalog
    {
        public bool TryGet(MemoryProfileKey key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MemoryProfileSnapshot? profile)
        {
            profile = snapshot is not null && snapshot.Key == key ? snapshot : null;
            return profile is not null;
        }

        public bool TryGet(MemoryProfileKey key, MemoryProfileVersion version, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MemoryProfileSnapshot? profile)
        {
            profile = snapshot is not null && snapshot.Key == key && snapshot.Version == version ? snapshot : null;
            return profile is not null;
        }
    }

    private sealed class SequentialIds: IIdentifierGenerator<RetrievalRequestId>
    {
        public RetrievalRequestId Create() => new(Guid.NewGuid());
    }

    private static RetrievalContextContributor Contributor(ScriptedPipeline pipeline, MemoryProfileSnapshot? snapshot, Action<RetrievalContextOptions>? configure = null, ILogger<RetrievalContextContributor>? logger = null)
    {
        var options = new RetrievalContextOptions();
        configure?.Invoke(options);
        return new RetrievalContextContributor(pipeline, new FixedCatalog(snapshot), new SequentialIds(), Options.Create(options), logger);
    }

    private static ContextContributionRequest Request(string? memoryProfile = "memory", params string[] userTexts)
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var agent = AgentDefinitionFixtures.Create(agentId, revision: 1, displayName: "agent") with
        {
            OptionalCapabilities = new AgentOptionalCapabilitySelection(
                null, null, null, memoryProfile is null ? null : new MemoryProfileKey(memoryProfile), null, []),
        };
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId);
        var authorization = TestSecurityEvidence.Authorization(agentId, sessionId, correlation, identity);
        var messages = userTexts
            .Select(text => (AgentMessage) new UserMessage(
                new MessageId(Guid.NewGuid()), agentId, sessionId, null, branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete,
                [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty))
            .ToImmutableArray();
        var history = new HistoryView(new MessageCursor(agentId, sessionId, null, branchId, new SessionVersion(1), new SessionSequence(0)), messages, []);
        return new ContextContributionRequest(
            agent, sessionId, null, identity, runId, turnId, new ModelRequestId(Guid.NewGuid()),
            new ModelDescriptor(
                new ModelAlias("chat"), new ProviderId("test"), new ApiFamilyId("test"), new ModelId("test"), null,
                new ModelCapabilities(true, true, true, true, true, true, true, ExtensionData.Empty), new ModelLimits(4096, 1024), null, ExtensionData.Empty),
            history, authorization, new EffectiveConfigurationSnapshot(new ConfigurationVersion(1), new ContentHash("sha256:configuration"), [], []));
    }

    private static RetrievalResult Completed(RetrievalQuery query, params RetrievalCandidate[] candidates) =>
        RetrievalResult.Completed(query.Id, [.. candidates], new RetrievalSummary(candidates.Length, 0, 0, 0, 0, 0, null));

    [Fact]
    public async Task ContributeAsync_WhenCandidatesAreReturned_ProjectsEachAsRetrievedReferenceData()
    {
        var memoryId = new MemoryId(Guid.NewGuid());
        var pipeline = new ScriptedPipeline(query => Completed(
            query,
            MemoryTestData.Candidate(query.Id, "first fact", 0.9, "tests.source", memoryId),
            MemoryTestData.Candidate(query.Id, "second fact", 0.5, "tests.source")));
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot());

        var contribution = await contributor.ContributeAsync(Request("memory", "what do I prefer?"), TestContext.Current.CancellationToken);

        contribution.Candidates.Length.ShouldBe(2);
        contribution.Candidates.ShouldAllBe(static candidate => candidate.Trust == ContextTrust.RetrievedData && candidate.Kind == ContextCandidateKind.ReferenceData && !candidate.Mandatory);
        contribution.Candidates[0].Priority.ShouldBeGreaterThan(contribution.Candidates[1].Priority);
        contribution.Candidates[0].Source.Key.Value.ShouldBe($"memory:{memoryId}");
        ((TextPart) contribution.Candidates[0].Content[0]).Text.ShouldBe("first fact");
        contribution.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task ContributeAsync_WhenACandidateIsProjected_PreservesProvenanceAsExtensionData()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query, MemoryTestData.Candidate(query.Id, "fact", 0.9, "tests.source")));
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot());

        var contribution = await contributor.ContributeAsync(Request("memory", "question"), TestContext.Current.CancellationToken);

        var extension = contribution.Candidates[0].Extensions.Values["agentkit.retrieval.provenance"];
        using var document = JsonDocument.Parse(extension.CanonicalJson.AsSpan().ToArray());
        document.RootElement.GetProperty("source").GetString().ShouldBe("tests.source");
        document.RootElement.GetProperty("sourceKind").GetString().ShouldBe("test");
        document.RootElement.GetProperty("classification").GetString().ShouldBe(nameof(DataClassification.Internal));
    }

    [Fact]
    public async Task ContributeAsync_WhenHistoryHasSeveralUserMessages_QueriesWithTheLatestUnderTheCapturedAuthorization()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query));
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot(), options => options.MaximumClassification = DataClassification.Public);
        var request = Request("memory", "older question", "latest question");

        _ = await contributor.ContributeAsync(request, TestContext.Current.CancellationToken);

        var query = pipeline.Queries.ShouldHaveSingleItem();
        query.Query.Text.ShouldBe("latest question");
        query.Context.Authorization.ShouldBe(request.Authorization);
        query.Context.ProfileKey.ShouldBe(new MemoryProfileKey("memory"));
        query.MaximumClassification.ShouldBe(DataClassification.Public);
        query.ExposureDestination!.ModelAlias.ShouldBe(new ModelAlias("chat"));
        query.Context.Identity.ShouldBe(request.Identity);
    }

    [Fact]
    public async Task ContributeAsync_WhenTheQueryIsTooLong_TruncatesItAtTheConfiguredBound()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query));
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot(), options => options.MaximumQueryCharacters = 5);

        _ = await contributor.ContributeAsync(Request("memory", "abcdefghij"), TestContext.Current.CancellationToken);

        pipeline.Queries.ShouldHaveSingleItem().Query.Text.ShouldBe("abcde");
    }

    [Fact]
    public async Task ContributeAsync_WhenTheAgentHasNoMemoryProfile_ContributesNothingWithoutQuerying()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query));
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot());

        var contribution = await contributor.ContributeAsync(Request(null, "question"), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
        pipeline.Queries.ShouldBeEmpty();
    }

    [Fact]
    public async Task ContributeAsync_WhenTheProfileIsUnknownToTheCatalog_ContributesNothingWithoutQuerying()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query));
        var contributor = Contributor(pipeline, snapshot: null);

        var contribution = await contributor.ContributeAsync(Request("memory", "question"), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
        pipeline.Queries.ShouldBeEmpty();
    }

    [Fact]
    public async Task ContributeAsync_WhenThereIsNoUserText_ContributesNothingWithoutQuerying()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query));
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot());

        var contribution = await contributor.ContributeAsync(Request("memory"), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
        pipeline.Queries.ShouldBeEmpty();
    }

    [Fact]
    public async Task ContributeAsync_WhenRetrievalIsRefused_ContributesNothingAndAddsAContentFreeDiagnostic()
    {
        var pipeline = new ScriptedPipeline(query => RetrievalResult.Failed(query.Id, new RetrievalFailure(RetrievalFailureKind.Denied, "secret policy detail")));
        var logger = new RecordingLogger<RetrievalContextContributor>();
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot(), logger: logger);

        var contribution = await contributor.ContributeAsync(Request("memory", "secret question"), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
        var diagnostic = contribution.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(ContextDiagnosticSeverity.Warning);
        diagnostic.Code.ShouldBe("retrieval-unavailable");
        diagnostic.SafeMessage.ShouldNotContain("secret");
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32400);
        entry.Message.ShouldNotContain("secret");
    }

    [Fact]
    public async Task ContributeAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var contributor = Contributor(new ScriptedPipeline(query => Completed(query)), MemoryTestData.Snapshot());

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await contributor.ContributeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task ContributeAsync_WhenAlreadyCancelled_PropagatesCancellationBeforeQuerying()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query));
        var contributor = Contributor(pipeline, MemoryTestData.Snapshot());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await contributor.ContributeAsync(Request("memory", "question"), source.Token));

        pipeline.Queries.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_Throws()
    {
        var pipeline = new ScriptedPipeline(query => Completed(query));
        var catalog = new FixedCatalog(null);
        var ids = new SequentialIds();
        var options = Options.Create(new RetrievalContextOptions());

        Should.Throw<ArgumentNullException>(() => new RetrievalContextContributor(null!, catalog, ids, options)).ParamName.ShouldBe("pipeline");
        Should.Throw<ArgumentNullException>(() => new RetrievalContextContributor(pipeline, null!, ids, options)).ParamName.ShouldBe("profiles");
        Should.Throw<ArgumentNullException>(() => new RetrievalContextContributor(pipeline, catalog, null!, options)).ParamName.ShouldBe("requestIds");
        Should.Throw<ArgumentNullException>(() => new RetrievalContextContributor(pipeline, catalog, ids, null!)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalContextContributor(pipeline, catalog, ids, Options.Create(new RetrievalContextOptions { MaximumItems = 0 }))).ParamName.ShouldBe("options");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalContextContributor(pipeline, catalog, ids, Options.Create(new RetrievalContextOptions { MaximumClassification = (DataClassification) 99 })));
    }
}
