// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ServiceExtensionsTests
{
    private sealed class FirstEvaluator: IEvaluator
    {
        public EvaluatorDescriptor Descriptor { get; } = new(new EvaluatorKey("first"), new EvaluatorVersion(1), "first", [], false);

        public ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<EvaluationOutcome>(new EvaluationPassed(null, "first"));
    }

    private sealed class SecondEvaluator: IEvaluator
    {
        public EvaluatorDescriptor Descriptor { get; } = new(new EvaluatorKey("first"), new EvaluatorVersion(2), "second", [], false);

        public ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<EvaluationOutcome>(new EvaluationPassed(null, "second"));
    }

    private sealed class FirstStore: IEvaluationResultStore
    {
        public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SecondStore: IEvaluationResultStore
    {
        public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FirstExporter: IEvaluationReportExporter
    {
        public ValueTask<EvaluationExportResult> ExportAsync(EvaluationReport report, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<EvaluationExportResult>(new EvaluationExported());
    }

    private sealed class SecondExporter: IEvaluationReportExporter
    {
        public ValueTask<EvaluationExportResult> ExportAsync(EvaluationReport report, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<EvaluationExportResult>(new EvaluationExported());
    }

    private sealed class CustomRunner: IEvaluationRunner
    {
        public Task<EvaluationReport> RunAsync(EvaluationPlan plan, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedRunIds: IIdentifierGenerator<EvaluationRunId>
    {
        public EvaluationRunId Create() => EvaluationTestData.RunId;
    }

    [Fact]
    public void AddAgentEvaluation_WhenServicesAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddAgentEvaluation(null!)).ParamName.ShouldBe("services");

    [Fact]
    public void AddAgentEvaluation_WhenCalled_RegistersTheRunnerAndReplaceableDefaultsButNoStoreEvaluatorOrExporter()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentEvaluation();

        services.Count(static d => d.ServiceType == typeof(IEvaluationRunner)).ShouldBe(1);
        services.ShouldContain(static d => d.ServiceType == typeof(IEvaluatorCatalog));
        services.ShouldContain(static d => d.ServiceType == typeof(IEvaluationResultStoreSelector));
        services.ShouldContain(static d => d.ServiceType == typeof(IEvaluationReportExporterCatalog));
        services.ShouldContain(static d => d.ServiceType == typeof(IIdentifierGenerator<EvaluationRunId>));
        services.ShouldNotContain(static d => d.IsKeyedService);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IIdentifierGenerator<EvaluationRunId>>().ShouldBeOfType<GuidEvaluationRunIdGenerator>();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void AddAgentEvaluation_WhenRepeated_RegistersOneRunnerAndComposesConfigurationInCallOrder()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentEvaluation(static o => o.MaximumConcurrentCases = 7).AddAgentEvaluation(static o => o.MaximumRepetitions = 3);

        services.Count(static d => d.ServiceType == typeof(IEvaluationRunner)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EvaluationOptions>>().Value;
        options.MaximumConcurrentCases.ShouldBe(7);
        options.MaximumRepetitions.ShouldBe(3);
    }

    [Theory]
    [InlineData(0, 1, 60)]
    [InlineData(1, 0, 60)]
    [InlineData(1, 1, 0)]
    public void AddAgentEvaluation_WhenOptionsAreInvalid_FailsWhenTheyAreFirstRead(int concurrency, int repetitions, int seconds)
    {
        var services = new ServiceCollection().AddAgentEvaluation(options =>
        {
            options.MaximumConcurrentCases = concurrency;
            options.MaximumRepetitions = repetitions;
            options.DefaultCaseTimeout = TimeSpan.FromSeconds(seconds);
        });
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EvaluationOptions>>().Value);
    }

    [Fact]
    public void AddAgentEvaluation_WhenAnotherRunnerIsAlreadyRegistered_FailsInsteadOfChoosingOne()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IEvaluationRunner, CustomRunner>();
        _ = services.AddSingleton<IEvaluationRunner, CustomRunner>();

        _ = Should.Throw<InvalidOperationException>(() => services.AddAgentEvaluation());
    }

    [Fact]
    public async Task AddAgentEvaluation_WhenResolvedInsideAnEngine_BindsThatEnginesRunner()
    {
        await using var harness = await EvaluationHarness.CreateAsync();

        _ = harness.Runner.ShouldBeOfType<EvaluationRunner>();
        harness.Engine.Services.GetRequiredService<IEvaluationRunner>().ShouldBeSameAs(harness.Runner);
    }

    [Fact]
    public void AddAgentEvaluation_WhenNoEngineIsRegistered_FailsWhenTheRunnerIsResolved()
    {
        using var provider = new ServiceCollection().AddAgentEvaluation().BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(provider.GetRequiredService<IEvaluationRunner>);

        exception.Message.ShouldContain("exactly one AgentEngine");
    }

    [Fact]
    public void AddAgentEvaluation_WhenTimeAndIdentityAreAlreadyRegistered_KeepsThoseReplacements()
    {
        var time = new FakeTimeProvider();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(time);
        _ = services.AddSingleton<IIdentifierGenerator<EvaluationRunId>, FixedRunIds>();

        using var provider = services.AddAgentEvaluation().BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(time);
        _ = provider.GetRequiredService<IIdentifierGenerator<EvaluationRunId>>().ShouldBeOfType<FixedRunIds>();
    }

    [Fact]
    public void ReplaceEvaluationRunner_WhenServicesAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.ReplaceEvaluationRunner<CustomRunner>(null!)).ParamName.ShouldBe("services");

    [Fact]
    public void ReplaceEvaluationRunner_WhenAfterAddAgentEvaluation_LeavesExactlyTheCustomRunner()
    {
        var services = new ServiceCollection().AddAgentEvaluation().ReplaceEvaluationRunner<CustomRunner>();

        services.Count(static d => d.ServiceType == typeof(IEvaluationRunner)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IEvaluationRunner>().ShouldBeOfType<CustomRunner>();
    }

    [Fact]
    public void ReplaceEvaluationRunner_WhenBeforeAddAgentEvaluation_IsNotOverriddenByTheDefault()
    {
        var services = new ServiceCollection().ReplaceEvaluationRunner<CustomRunner>().AddAgentEvaluation();

        services.Count(static d => d.ServiceType == typeof(IEvaluationRunner)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IEvaluationRunner>().ShouldBeOfType<CustomRunner>();
    }

    [Fact]
    public void AddEvaluator_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddEvaluator<FirstEvaluator>(null!, new EvaluatorKey("first"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddEvaluator<FirstEvaluator>(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddEvaluator_WhenRepeatedIdentically_IsIdempotent()
    {
        var services = new ServiceCollection().AddEvaluator<FirstEvaluator>(new EvaluatorKey("first")).AddEvaluator<FirstEvaluator>(new EvaluatorKey("first"));

        services.Count(static d => d.ServiceType == typeof(IEvaluator)).ShouldBe(1);
    }

    [Fact]
    public void AddEvaluator_WhenAKeyIsReusedForAnotherImplementation_FailsInsteadOfChoosingTheLastRegistration()
    {
        var services = new ServiceCollection().AddEvaluator<FirstEvaluator>(new EvaluatorKey("first"));

        var exception = Should.Throw<InvalidOperationException>(() => services.AddEvaluator<SecondEvaluator>(new EvaluatorKey("first")));

        exception.Message.ShouldContain("first");
        services.Count(static d => d.ServiceType == typeof(IEvaluator)).ShouldBe(1);
    }

    [Fact]
    public void AddEvaluator_WhenKeysDiffer_AddsEvaluatorsAdditivelyAndTheCatalogFindsEach()
    {
        var services = new ServiceCollection()
            .AddEvaluator<FirstEvaluator>(new EvaluatorKey("first"))
            .AddKeyedSingleton<IEvaluator>("other", new ScriptedEvaluator("other"));
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IEvaluatorCatalog>();

        _ = catalog.Find(new EvaluatorKey("first")).ShouldBeOfType<FirstEvaluator>();
        _ = catalog.Find(new EvaluatorKey("other")).ShouldBeOfType<ScriptedEvaluator>();
        catalog.Find(new EvaluatorKey("absent")).ShouldBeNull();
    }

    [Fact]
    public void ReplaceEvaluator_WhenAKeyIsRegistered_ReplacesOnlyThatKey()
    {
        var services = new ServiceCollection()
            .AddEvaluator<FirstEvaluator>(new EvaluatorKey("first"))
            .AddKeyedSingleton<IEvaluator>("other", new ScriptedEvaluator("other"))
            .ReplaceEvaluator<SecondEvaluator>(new EvaluatorKey("first"));
        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IEvaluatorCatalog>();

        _ = catalog.Find(new EvaluatorKey("first")).ShouldBeOfType<SecondEvaluator>();
        _ = catalog.Find(new EvaluatorKey("other")).ShouldBeOfType<ScriptedEvaluator>();
        services.Count(static d => d.ServiceType == typeof(IEvaluator)).ShouldBe(2);
    }

    [Fact]
    public void ReplaceEvaluator_WhenNothingIsRegisteredUnderTheKey_AddsIt()
    {
        using var provider = new ServiceCollection().ReplaceEvaluator<FirstEvaluator>(new EvaluatorKey("first")).BuildServiceProvider();

        _ = provider.GetRequiredService<IEvaluatorCatalog>().Find(new EvaluatorKey("first")).ShouldBeOfType<FirstEvaluator>();
    }

    [Fact]
    public void ReplaceEvaluator_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.ReplaceEvaluator<FirstEvaluator>(null!, new EvaluatorKey("first"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentException>(() => new ServiceCollection().ReplaceEvaluator<FirstEvaluator>(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public async Task AddEvaluationResultStore_WhenRegisteredUnderAKey_IsSelectedOnlyByThatKey()
    {
        var services = new ServiceCollection().AddEvaluationResultStore<FirstStore>(new EvaluationResultStoreKey("durable"));
        using var provider = services.BuildServiceProvider();
        var selector = provider.GetRequiredService<IEvaluationResultStoreSelector>();

        _ = (await selector.SelectAsync(new EvaluationResultStoreKey("durable"), TestContext.Current.CancellationToken)).ShouldBeOfType<EvaluationResultStoreSelected>().Store.ShouldBeOfType<FirstStore>();
        _ = (await selector.SelectAsync(new EvaluationResultStoreKey("other"), TestContext.Current.CancellationToken)).ShouldBeOfType<EvaluationResultStoreUnavailable>();
    }

    [Fact]
    public void AddEvaluationResultStore_WhenRepeatedOrConflicting_IsIdempotentOrFails()
    {
        var services = new ServiceCollection().AddEvaluationResultStore<FirstStore>(new EvaluationResultStoreKey("durable"));

        _ = services.AddEvaluationResultStore<FirstStore>(new EvaluationResultStoreKey("durable"));
        _ = Should.Throw<InvalidOperationException>(() => services.AddEvaluationResultStore<SecondStore>(new EvaluationResultStoreKey("durable")));

        services.Count(static d => d.ServiceType == typeof(IEvaluationResultStore)).ShouldBe(1);
    }

    [Fact]
    public void AddEvaluationResultStore_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddEvaluationResultStore<FirstStore>(null!, new EvaluationResultStoreKey("s"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddEvaluationResultStore<FirstStore>(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public void AddEvaluationReportExporter_WhenRegisteredUnderAKey_IsFoundOnlyByThatKey()
    {
        using var provider = new ServiceCollection().AddEvaluationReportExporter<FirstExporter>(new EvaluationReportExporterKey("json")).BuildServiceProvider();
        var catalog = provider.GetRequiredService<IEvaluationReportExporterCatalog>();

        _ = catalog.Find(new EvaluationReportExporterKey("json")).ShouldBeOfType<FirstExporter>();
        catalog.Find(new EvaluationReportExporterKey("other")).ShouldBeNull();
    }

    [Fact]
    public void AddEvaluationReportExporter_WhenRepeatedOrConflicting_IsIdempotentOrFails()
    {
        var services = new ServiceCollection().AddEvaluationReportExporter<FirstExporter>(new EvaluationReportExporterKey("json"));

        _ = services.AddEvaluationReportExporter<FirstExporter>(new EvaluationReportExporterKey("json"));
        _ = Should.Throw<InvalidOperationException>(() => services.AddEvaluationReportExporter<SecondExporter>(new EvaluationReportExporterKey("json")));

        services.Count(static d => d.ServiceType == typeof(IEvaluationReportExporter)).ShouldBe(1);
    }

    [Fact]
    public void AddEvaluationReportExporter_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddEvaluationReportExporter<FirstExporter>(null!, new EvaluationReportExporterKey("e"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddEvaluationReportExporter<FirstExporter>(default)).ParamName.ShouldBe("key");
    }

    [Fact]
    public async Task AddEvaluationResultStore_WhenNothingIsRegistered_KeepsThePlanFromPersistingAnywhere()
    {
        await using var harness = await EvaluationHarness.CreateAsync();
        var plan = EvaluationTestData.Plan(recording: new EvaluationRecordingPolicy(new EvaluationResultStoreKey("durable"), []));

        var exception = await Should.ThrowAsync<EvaluationPlanRejectedException>(() => harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken));

        exception.Problems.Single().Kind.ShouldBe(EvaluationPlanProblemKind.DestinationUnavailable);
    }

    [Fact]
    public void AddModelJudgeEvaluator_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddModelJudgeEvaluator(null!, static o => o.JudgeModel = new ModelAlias("j"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddModelJudgeEvaluator(null!)).ParamName.ShouldBe("configure");
    }

    [Fact]
    public void AddModelJudgeEvaluator_WhenNoJudgeModelIsNamed_FailsAtRegistration() =>
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddModelJudgeEvaluator(static _ => { })).ParamName.ShouldBe("judgeModel");

    [Fact]
    public void AddModelJudgeEvaluator_WhenABoundIsInvalid_FailsAtRegistration() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddModelJudgeEvaluator(static o =>
        {
            o.JudgeModel = new ModelAlias("j");
            o.RepeatCount = 0;
        })).ParamName.ShouldBe("repeatCount");

    [Fact]
    public void AddModelJudgeEvaluator_WhenResolved_UsesTheExplicitSettingsAndTheRegisteredClient()
    {
        var client = new RecordedModelJudgeClient();
        var services = new ServiceCollection()
            .AddSingleton<IModelJudgeClient>(client)
            .AddModelJudgeEvaluator(static o =>
            {
                o.JudgeModel = new ModelAlias("judge");
                o.RepeatCount = 5;
            });
        using var provider = services.BuildServiceProvider();

        var evaluator = provider.GetRequiredService<IEvaluatorCatalog>().Find(ModelJudgeEvaluator.Key).ShouldBeOfType<ModelJudgeEvaluator>();

        evaluator.Descriptor.Key.ShouldBe(ModelJudgeEvaluator.Key);
        provider.GetRequiredService<IEvaluatorCatalog>().Find(ModelJudgeEvaluator.Key).ShouldBeSameAs(evaluator);
    }

    [Fact]
    public void AddModelJudgeEvaluator_WhenRepeatedWithTheSameSettings_IsIdempotentAndWithDifferentSettingsFails()
    {
        var services = new ServiceCollection().AddModelJudgeEvaluator(static o => o.JudgeModel = new ModelAlias("judge"));

        _ = services.AddModelJudgeEvaluator(static o => o.JudgeModel = new ModelAlias("judge"));
        _ = Should.Throw<InvalidOperationException>(() => services.AddModelJudgeEvaluator(static o => o.JudgeModel = new ModelAlias("another")));

        services.Count(static d => d.ServiceType == typeof(IEvaluator)).ShouldBe(1);
    }

    [Fact]
    public void AddModelJudgeEvaluator_WhenTheKeyBelongsToAnotherEvaluator_FailsInsteadOfChoosing()
    {
        var services = new ServiceCollection().AddKeyedSingleton<IEvaluator>("model-judge", new ScriptedEvaluator("model-judge"));

        _ = Should.Throw<InvalidOperationException>(() => services.AddModelJudgeEvaluator(static o => o.JudgeModel = new ModelAlias("judge")));
    }

    [Fact]
    public void AddModelRequestJudgeClient_WhenServicesAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ServiceExtensions.AddModelRequestJudgeClient(null!)).ParamName.ShouldBe("services");

    [Fact]
    public void AddModelRequestJudgeClient_WhenProviderServicesExist_ResolvesTheFirstPartyClient()
    {
        var descriptor = ModelDescriptorFixtures.Chat("judge");
        var services = new ServiceCollection()
            .AddSingleton<IModelCatalog>(new StaticModelCatalog(ModelDescriptorFixtures.Catalog("judge")))
            .AddSingleton<IModelSelector>(ScriptedModelSelector.Selecting(descriptor))
            .AddSingleton<ILlmModelResolver>(new AliasLlmModelResolver())
            .AddModelRequestJudgeClient(static o => o.MaximumOutputTokens = 32);
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IModelJudgeClient>().ShouldBeOfType<ModelRequestJudgeClient>();
    }

    [Fact]
    public void AddModelRequestJudgeClient_WhenACustomClientIsAlreadyRegistered_KeepsIt()
    {
        var custom = new RecordedModelJudgeClient();
        var services = new ServiceCollection().AddSingleton<IModelJudgeClient>(custom).AddModelRequestJudgeClient();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IModelJudgeClient>().ShouldBeSameAs(custom);
    }
}
