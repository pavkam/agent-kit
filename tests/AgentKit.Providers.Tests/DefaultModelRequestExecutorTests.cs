// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit;
using AgentKit.TestSupport;

using Microsoft.Extensions.Options;

/// <summary>Verifies <see cref="DefaultModelRequestExecutor"/>.</summary>
public sealed class DefaultModelRequestExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenFirstAttemptSucceeds_ReturnsCompleted()
    {
        var alias = new ModelAlias("chat");
        var model = new ScriptedLlmModel(alias, CompletedAttempt());
        var executor = CreateExecutor(model, allowSemanticFallback: false);

        var result = await executor.ExecuteAsync(
            ExecutionRequest(alias, retryAttempts: 1),
            NullModelResponseObserver.Instance,
            TestContext.Current.CancellationToken);

        var completed = result.ShouldBeOfType<ModelExecutionCompleted>();
        completed.Attempts.ShouldBe(1);
        model.ReceivedRequests.Count.ShouldBe(1);
        model.ReceivedRequests[0].Attempt.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryableFailureHasNoObserverEvents_RetriesAndCompletes()
    {
        var alias = new ModelAlias("chat");
        var model = new ScriptedLlmModel(
            alias,
            FailedAttempt(ProviderFailureKind.Unavailable),
            CompletedAttempt());
        var executor = CreateExecutor(model, allowSemanticFallback: false);

        var result = await executor.ExecuteAsync(
            ExecutionRequest(
                alias,
                retryAttempts: 2,
                initialDelay: TimeSpan.Zero,
                maximumDelay: TimeSpan.FromSeconds(5)),
            NullModelResponseObserver.Instance,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ModelExecutionCompleted>();
        model.ReceivedRequests.Count.ShouldBe(2);
        model.ReceivedRequests[1].Attempt.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenObserverSawAnEvent_DoesNotRetrySameModel()
    {
        var alias = new ModelAlias("chat");
        var model = new EmittingFailureLlmModel(alias);
        var executor = CreateExecutor(model, allowSemanticFallback: false);

        var result = await executor.ExecuteAsync(
            ExecutionRequest(
                alias,
                retryAttempts: 3,
                initialDelay: TimeSpan.Zero,
                maximumDelay: TimeSpan.FromSeconds(1)),
            NullModelResponseObserver.Instance,
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ModelExecutionFailed>();
        model.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrderedFallbackIsAllowed_ReturnsFallbackRequired()
    {
        var alias = new ModelAlias("chat");
        var model = new ScriptedLlmModel(alias, FailedAttempt(ProviderFailureKind.Throttling));
        var executor = CreateExecutor(model, allowSemanticFallback: true);

        var result = await executor.ExecuteAsync(
            ExecutionRequest(
                alias,
                retryAttempts: 1,
                fallback: ModelFallbackPolicy.OrderedCandidates),
            NullModelResponseObserver.Instance,
            TestContext.Current.CancellationToken);

        var fallback = result.ShouldBeOfType<ModelFallbackRequired>();
        fallback.Failure.Kind.ShouldBe(ProviderFailureKind.Throttling);
        fallback.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFallbackIsDisabled_ReturnsTerminalFailure()
    {
        var alias = new ModelAlias("chat");
        var model = new ScriptedLlmModel(alias, FailedAttempt(ProviderFailureKind.Timeout));
        var executor = CreateExecutor(model, allowSemanticFallback: false);

        var result = await executor.ExecuteAsync(
            ExecutionRequest(
                alias,
                retryAttempts: 1,
                fallback: ModelFallbackPolicy.OrderedCandidates),
            NullModelResponseObserver.Instance,
            TestContext.Current.CancellationToken);

        var failed = result.ShouldBeOfType<ModelExecutionFailed>();
        failed.Failure.Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAdapterIsMissing_ReturnsFailedWithoutCallingProvider()
    {
        var executor = CreateExecutor(allowSemanticFallback: false);

        var result = await executor.ExecuteAsync(
            ExecutionRequest(new ModelAlias("missing"), retryAttempts: 1),
            NullModelResponseObserver.Instance,
            TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ModelExecutionFailed>().Failure.Kind.ShouldBe(ProviderFailureKind.InvalidRequest);
    }

    [Fact]
    public void AddAgentProviders_WhenReplaceModelRequestExecutorIsUsed_ResolvesReplacement()
    {
        using var provider = BuildProvider(services =>
            services.ReplaceModelRequestExecutor<StubExecutor>());

        _ = provider.GetRequiredService<IModelRequestExecutor>().ShouldBeOfType<StubExecutor>();
    }

    private static DefaultModelRequestExecutor CreateExecutor(
        ILlmModel? model = null,
        bool allowSemanticFallback = false)
    {
        ILlmModel[] models = model is null ? [] : [model];
        var resolver = new DefaultLlmModelResolver(models);
        var options = Options.Create(new AgentProviderRuntimeOptions { AllowSemanticFallback = allowSemanticFallback });
        return new DefaultModelRequestExecutor(
            resolver,
            options,
            TimeProvider.System,
            NullLogger<DefaultModelRequestExecutor>.Instance);
    }

    private static ModelExecutionRequest ExecutionRequest(
        ModelAlias alias,
        int retryAttempts,
        TimeSpan? initialDelay = null,
        TimeSpan? maximumDelay = null,
        ModelFallbackPolicy fallback = ModelFallbackPolicy.FirstCandidateOnly)
    {
        var descriptor = ProviderTestData.Model(alias.Value);
        var selection = new ModelSelectionDecision(
            descriptor,
            new ModelCatalogVersion(1),
            "selected",
            []);
        var context = new LlmRequestContext(
            ProviderTestData.ModelRequestId,
            descriptor,
            [],
            [],
            LlmToolChoice.None,
            new LlmRequestSettings(null, null, null, [], null, null, ExtensionData.Empty),
            ExtensionData.Empty);
        var operation = ProtectedOperation();
        var retryPolicy = new ProviderRetryPolicy(
            retryAttempts,
            initialDelay ?? TimeSpan.Zero,
            maximumDelay ?? TimeSpan.FromSeconds(30));
        return new ModelExecutionRequest(operation, selection, context, budget: null, hooks: null, retryPolicy, fallback);
    }

    private static ProtectedSemanticOperationContext ProtectedOperation()
    {
        var agentId = ProviderTestData.AgentId;
        var identity = TestExecutionIdentity.Create(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            ExecutionSubjectKind.Service);
        var correlation = new BeforeRunOperationCorrelation(ProviderTestData.OperationId, admissionId: null);
        return new ProtectedSemanticOperationContext(
            agentId,
            ProviderTestData.SessionId,
            conversationId: null,
            identity,
            correlation,
            TestSecurityEvidence.Authorization(agentId, ProviderTestData.SessionId, correlation, identity));
    }

    private static ModelAttemptCompleted CompletedAttempt() =>
        new(new ModelResponse(
            ProviderTestData.ModelRequestId,
            new ProviderResponseIdentity(
                new ProviderId("test-provider"),
                null,
                new ApiFamilyId("test-family"),
                new ModelId("chat-model"),
                new ModelId("chat-model"),
                null,
                null,
                null),
            [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)],
            NormalizedStopReason.Completed,
            ModelUsage.NotReported,
            ExtensionData.Empty));

    private static ModelAttemptFailed FailedAttempt(ProviderFailureKind kind) =>
        new(
            new ProviderFailure(
                kind,
                new ProviderId("test-provider"),
                null,
                null,
                null,
                null,
                kind.ToString(),
                null,
                ExtensionData.Empty),
            [],
            usage: null);

    private static ServiceProvider BuildProvider(Func<IServiceCollection, IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = configure(services.AddAgentProviders());
        return services.BuildServiceProvider();
    }

    private sealed class StubExecutor: IModelRequestExecutor
    {
        public Task<ModelExecutionResult> ExecuteAsync(
            ModelExecutionRequest request,
            IModelResponseObserver observer,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ModelExecutionResult>(
                new ModelExecutionFailed(
                    request.Selection,
                    1,
                    new ProviderFailure(
                        ProviderFailureKind.Unknown,
                        new ProviderId("test"),
                        null,
                        null,
                        null,
                        null,
                        "stub",
                        null,
                        ExtensionData.Empty)));
    }

    private sealed class NullModelResponseObserver: IModelResponseObserver
    {
        public static NullModelResponseObserver Instance { get; } = new();

        public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class EmittingFailureLlmModel(ModelAlias alias): ILlmModel
    {
        public ModelAlias Alias { get; } = alias;

        public int CallCount { get; private set; }

        public async Task<ModelAttemptResult> ExecuteAsync(
            LlmModelRequest request,
            IModelResponseObserver observer,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await observer.OnEventAsync(
                new ModelResponseStarted(request.Context.ModelRequestId, sequence: 0),
                cancellationToken).ConfigureAwait(false);
            return FailedAttempt(ProviderFailureKind.Unavailable);
        }
    }
}
