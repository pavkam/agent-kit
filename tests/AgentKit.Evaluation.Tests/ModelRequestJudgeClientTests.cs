// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelRequestJudgeClientTests
{
    private static readonly ProviderResponseIdentity _identity =
        new(new ProviderId("recorded-provider"), null, new ApiFamilyId("recorded-api"), new ModelId("recorded-model"), new ModelId("recorded-model-2026"), null, null, null);

    private sealed class Sequential<TId>(Func<Guid, TId> create): IIdentifierGenerator<TId>
        where TId : struct
    {
        private int _next;

        public TId Create() => create(new Guid(Interlocked.Increment(ref _next), 0, 0, [0, 0, 0, 0, 0, 0, 0, 7]));
    }

    private sealed record Rig(ModelRequestJudgeClient Client, ScriptedLlmModel Model, ScriptedModelSelector Selector, FakeTimeProvider Time);

    private static ModelAttemptCompleted Completed(string text, NormalizedStopReason stop = NormalizedStopReason.Completed, ImmutableArray<ContentPart>? extra = null) =>
        new(new ModelResponse(
            new ModelRequestId(Guid.NewGuid()),
            _identity,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty), .. extra ?? []],
            stop,
            new ModelUsage(ModelUsageReportState.Final, 120, 15, null, null, null, null, ExtensionData.Empty),
            ExtensionData.Empty));

    private static ProviderFailure Failure(ProviderFailureKind kind) =>
        new(kind, new ProviderId("recorded-provider"), null, null, null, null, "safe", null, ExtensionData.Empty);

    private static Rig Build(
        ModelAttemptResult? attempt = null,
        ModelSelectionResult? selection = null,
        bool resolvable = true,
        ModelRequestJudgeClientOptions? options = null)
    {
        var descriptor = ModelDescriptorFixtures.Chat("judge-model");
        var model = new ScriptedLlmModel(new ModelAlias("judge-model"), attempt ?? Completed(/*lang=json,strict*/ """{"score": 4, "reason": "fine"}"""));
        var selector = selection is null ? ScriptedModelSelector.Selecting(descriptor) : new ScriptedModelSelector(selection);
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch.AddDays(2));
        var client = new ModelRequestJudgeClient(
            new StaticModelCatalog(ModelDescriptorFixtures.Catalog("judge-model")),
            selector,
            resolvable ? new AliasLlmModelResolver(model) : new AliasLlmModelResolver(),
            new Sequential<ModelRequestId>(static id => new ModelRequestId(id)),
            new Sequential<OperationId>(static id => new OperationId(id)),
            time,
            options ?? new ModelRequestJudgeClientOptions());
        return new Rig(client, model, selector, time);
    }

    private static ModelJudgeRequest Request(AgentRunResult<ValidatedOutput>? result = null) =>
        new(EvaluationTestData.Context(result), new ModelAlias("judge-model"), "INSTRUCTIONS", "<candidate>\nanswer\n</candidate>", 1);

    [Fact]
    public void Constructor_WhenAnArgumentIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        var catalog = new StaticModelCatalog(ModelDescriptorFixtures.Catalog());
        var selector = ScriptedModelSelector.Selecting(ModelDescriptorFixtures.Chat());
        var resolver = new AliasLlmModelResolver();
        var requestIds = new Sequential<ModelRequestId>(static id => new ModelRequestId(id));
        var operationIds = new Sequential<OperationId>(static id => new OperationId(id));
        var time = new FakeTimeProvider();
        var options = new ModelRequestJudgeClientOptions();

        Should.Throw<ArgumentNullException>(() => new ModelRequestJudgeClient(null!, selector, resolver, requestIds, operationIds, time, options)).ParamName.ShouldBe("catalog");
        Should.Throw<ArgumentNullException>(() => new ModelRequestJudgeClient(catalog, null!, resolver, requestIds, operationIds, time, options)).ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(() => new ModelRequestJudgeClient(catalog, selector, null!, requestIds, operationIds, time, options)).ParamName.ShouldBe("resolver");
        Should.Throw<ArgumentNullException>(() => new ModelRequestJudgeClient(catalog, selector, resolver, null!, operationIds, time, options)).ParamName.ShouldBe("modelRequestIds");
        Should.Throw<ArgumentNullException>(() => new ModelRequestJudgeClient(catalog, selector, resolver, requestIds, null!, time, options)).ParamName.ShouldBe("operationIds");
        Should.Throw<ArgumentNullException>(() => new ModelRequestJudgeClient(catalog, selector, resolver, requestIds, operationIds, null!, options)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ModelRequestJudgeClient(catalog, selector, resolver, requestIds, operationIds, time, null!)).ParamName.ShouldBe("options");
    }

    [Fact]
    public void Constructor_WhenAnOptionIsOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Build(options: new ModelRequestJudgeClientOptions { RequestTimeout = TimeSpan.Zero }));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Build(options: new ModelRequestJudgeClientOptions { MaximumOutputTokens = 0 }));
        Should.Throw<ArgumentOutOfRangeException>(() => Build(options: new ModelRequestJudgeClientOptions { Temperature = 2.5 })).ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => Build(options: new ModelRequestJudgeClientOptions { Temperature = -0.1 })).ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task JudgeAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var rig = Build();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await rig.Client.JudgeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task JudgeAsync_WhenTheProviderAnswers_ReturnsTheRecordedReplyWithProviderModelAndUsage()
    {
        var rig = Build();

        var response = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        var completed = response.ShouldBeOfType<ModelJudgeCompleted>();
        completed.Text.ShouldBe(/*lang=json,strict*/ """{"score": 4, "reason": "fine"}""");
        completed.Provider.ShouldBe("recorded-provider");
        completed.ResolvedModel.ShouldBe("recorded-model-2026");
        (completed.InputTokens, completed.OutputTokens).ShouldBe((120L, 15L));
    }

    [Fact]
    public async Task JudgeAsync_WhenBuildingTheRequest_SendsToolFreeSystemAndDelimitedUserMessagesUnderBoundedSettings()
    {
        var rig = Build(options: new ModelRequestJudgeClientOptions { RequestTimeout = TimeSpan.FromSeconds(30), MaximumOutputTokens = 64, Temperature = 0.2 });

        _ = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        var sent = rig.Model.ReceivedRequests.Single();
        sent.Context.Tools.ShouldBeEmpty();
        sent.Context.ToolChoice.ShouldBe(LlmToolChoice.None);
        sent.Context.Settings.Temperature.ShouldBe(0.2);
        sent.Context.Settings.MaxOutputTokens.ShouldBe(64);
        sent.Attempt.ShouldBe(1);
        sent.Deadline.ShouldBe(rig.Time.GetUtcNow() + TimeSpan.FromSeconds(30));
        var system = sent.Context.Messages[0].ShouldBeOfType<SystemMessage>();
        system.Parts.OfType<TextPart>().Single().Text.ShouldBe("INSTRUCTIONS");
        var user = sent.Context.Messages[1].ShouldBeOfType<UserMessage>();
        user.Parts.OfType<TextPart>().Single().Text.ShouldBe("<candidate>\nanswer\n</candidate>");
        sent.Context.Model.Alias.ShouldBe(new ModelAlias("judge-model"));
    }

    [Fact]
    public async Task JudgeAsync_WhenSelectingTheModel_NamesOnlyTheExplicitAliasAndCorrelatesToTheJudgedRun()
    {
        var rig = Build();
        var request = Request();

        _ = await rig.Client.JudgeAsync(request, TestContext.Current.CancellationToken);

        var selection = rig.Selector.LastRequest.ShouldNotBeNull();
        selection.Policy.Candidates.ShouldBe([new ModelAlias("judge-model")]);
        selection.Requirements.RequiresSystemInstructions.ShouldBeTrue();
        selection.Scope.AgentId.ShouldBe(request.Context.Finished!.AgentId);
        selection.Scope.SessionId.ShouldBe(request.Context.Finished.SessionId);
        var correlation = selection.Scope.Correlation.ShouldBeOfType<AfterRunOperationCorrelation>();
        correlation.CausalRunId.ShouldBe(request.Context.Finished.RunId);
    }

    [Fact]
    public async Task JudgeAsync_WhenNoModelSatisfiesTheAlias_ReportsNotConfiguredWithoutCallingAProvider()
    {
        var rig = Build(selection: new InvalidModelPolicy("no such alias"));

        var response = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<ModelJudgeFailed>().Kind.ShouldBe(ModelJudgeFailureKind.NotConfigured);
        rig.Model.ReceivedRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task JudgeAsync_WhenNoAdapterIsRegisteredForTheModel_ReportsNotConfigured()
    {
        var rig = Build(resolvable: false);

        var response = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<ModelJudgeFailed>().Kind.ShouldBe(ModelJudgeFailureKind.NotConfigured);
    }

    [Fact]
    public async Task JudgeAsync_WhenTheJudgedRunProducedNoResult_ReportsNotConfigured()
    {
        var rig = Build();

        var response = await rig.Client.JudgeAsync(Request(EvaluationTestData.Rejected()), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<ModelJudgeFailed>().Kind.ShouldBe(ModelJudgeFailureKind.NotConfigured);
        rig.Selector.SelectCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(NormalizedStopReason.Length)]
    [InlineData(NormalizedStopReason.ToolUse)]
    public async Task JudgeAsync_WhenTheModelStopsBeforeCompleting_ReportsIncomplete(NormalizedStopReason stop)
    {
        var rig = Build(Completed("partial", stop));

        var response = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<ModelJudgeFailed>().Kind.ShouldBe(ModelJudgeFailureKind.Incomplete);
    }

    [Fact]
    public async Task JudgeAsync_WhenTheModelRequestsATool_ReportsIncompleteEvenIfItClaimsCompletion()
    {
        var call = EvaluationTestData.ToolCall("sneaky");
        var rig = Build(Completed("text", extra: [call]));

        var response = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<ModelJudgeFailed>().Kind.ShouldBe(ModelJudgeFailureKind.Incomplete);
    }

    [Fact]
    public async Task JudgeAsync_WhenTheProviderFails_ReportsUnavailableWithoutLeakingProviderDetail()
    {
        var rig = Build(new ModelAttemptFailed(Failure(ProviderFailureKind.Unavailable), [], null));

        var response = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        var failed = response.ShouldBeOfType<ModelJudgeFailed>();
        failed.Kind.ShouldBe(ModelJudgeFailureKind.Unavailable);
        failed.SafeMessage.ShouldNotContain("recorded-provider");
    }

    [Fact]
    public async Task JudgeAsync_WhenTheCallerCancelsAndTheProviderReportsCancellation_PropagatesCancellation()
    {
        var rig = Build(new ModelAttemptCancelled(Failure(ProviderFailureKind.Cancellation), [], null));
        using var cancellation = new CancellationTokenSource();
        rig.Model.ExecuteOverride = async (_, _) =>
        {
            await cancellation.CancelAsync();
            return new ModelAttemptCancelled(Failure(ProviderFailureKind.Cancellation), [], null);
        };

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await rig.Client.JudgeAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task JudgeAsync_WhenTheProviderCancelsWithoutTheCaller_ReportsUnavailable()
    {
        var rig = Build(new ModelAttemptCancelled(Failure(ProviderFailureKind.Cancellation), [], null));

        var response = await rig.Client.JudgeAsync(Request(), TestContext.Current.CancellationToken);

        response.ShouldBeOfType<ModelJudgeFailed>().Kind.ShouldBe(ModelJudgeFailureKind.Unavailable);
    }

    [Fact]
    public async Task JudgeAsync_WhenTokenIsAlreadyCancelled_ThrowsBeforeSelectingAModel()
    {
        var rig = Build();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await rig.Client.JudgeAsync(Request(), cancellation.Token));

        rig.Selector.SelectCount.ShouldBe(0);
    }

    [Fact]
    public async Task JudgeAsync_WhenFedThroughTheEvaluatorWithARecordedReply_ProducesTheRecordedVerdict()
    {
        var rig = Build(Completed(/*lang=json,strict*/ """{"score": 5, "reason": "perfect"}"""));
        var evaluator = new ModelJudgeEvaluator(rig.Client, new ModelJudgeSettings(new ModelAlias("judge-model"), 1, new ModelJudgeBudget(3), 0.25, 1_000));
        var context = EvaluationTestData.ContextFor(EvaluationTestData.Finished("Paris."), new RubricCriterion("Names Paris."));

        var outcome = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        var passed = outcome.ShouldBeOfType<EvaluationPassed>();
        passed.Score!.Value.ShouldBe(1d);
        passed.Evidence.Single(static e => e.Name == "judge.resolved_model").Value.ShouldBe("recorded-model-2026");
    }
}
