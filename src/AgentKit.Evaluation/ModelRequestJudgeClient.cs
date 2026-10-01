// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Is the first-party <see cref="IModelJudgeClient"/> over the provider-neutral model catalog, selector, and model adapter.</summary>
/// <remarks>
/// <para>
/// Each sample selects the explicit judge alias through <see cref="IModelSelector"/> against the current catalog snapshot, then
/// executes one request through the resolved <see cref="ILlmModel"/> with no tools. The judged run supplies only the identity
/// scope: the request is correlated to it as an after-run operation, and the judge conversation is never appended to its session.
/// A provider failure becomes <see cref="ModelJudgeFailed"/>; cancellation propagates.
/// </para>
/// <para>The instance is stateless and thread-safe. Provider credentials, egress authority, and retries belong to the resolved adapter and its profile, not to this client.</para>
/// </remarks>
public sealed class ModelRequestJudgeClient: IModelJudgeClient
{
    private static readonly ModelRequirements _requirements = new() { RequiresSystemInstructions = true };

    private readonly IModelCatalog _catalog;
    private readonly IModelSelector _selector;
    private readonly ILlmModelResolver _resolver;
    private readonly IIdentifierGenerator<ModelRequestId> _modelRequestIds;
    private readonly IIdentifierGenerator<OperationId> _operationIds;
    private readonly TimeProvider _timeProvider;
    private readonly ModelRequestJudgeClientOptions _options;

    /// <summary>Initializes the client.</summary>
    /// <param name="catalog">The model catalog the judge alias is looked up in.</param>
    /// <param name="selector">The selector that applies capability requirements.</param>
    /// <param name="resolver">The resolver of the model adapter that executes the request.</param>
    /// <param name="modelRequestIds">The generator of one model request identity per sample.</param>
    /// <param name="operationIds">The generator of one operation identity per sample.</param>
    /// <param name="timeProvider">The injected clock used for request deadlines.</param>
    /// <param name="options">The validated client options, copied at construction.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
    public ModelRequestJudgeClient(
        IModelCatalog catalog,
        IModelSelector selector,
        ILlmModelResolver resolver,
        IIdentifierGenerator<ModelRequestId> modelRequestIds,
        IIdentifierGenerator<OperationId> operationIds,
        TimeProvider timeProvider,
        ModelRequestJudgeClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(modelRequestIds);
        ArgumentNullException.ThrowIfNull(operationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.RequestTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumOutputTokens);
        if (options.Temperature is { } temperature)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(temperature, 0d, nameof(options));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(temperature, 2d, nameof(options));
        }

        _catalog = catalog;
        _selector = selector;
        _resolver = resolver;
        _modelRequestIds = modelRequestIds;
        _operationIds = operationIds;
        _timeProvider = timeProvider;
        _options = new ModelRequestJudgeClientOptions
        {
            RequestTimeout = options.RequestTimeout,
            MaximumOutputTokens = options.MaximumOutputTokens,
            Temperature = options.Temperature,
        };
    }

    /// <inheritdoc/>
    public async ValueTask<ModelJudgeResponse> JudgeAsync(ModelJudgeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Context.Finished is not { } finished)
        {
            return new ModelJudgeFailed(ModelJudgeFailureKind.NotConfigured, "The judged run has no result to correlate the judge request with.");
        }

        var modelRequestId = _modelRequestIds.Create();
        var scope = new SecurityAuthorizationScope(
            finished.AgentId,
            finished.SessionId,
            new AfterRunOperationCorrelation(_operationIds.Create(), finished.RunId));
        var snapshot = await _catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var selection = await _selector.SelectAsync(
            new ModelSelectionRequest(scope, modelRequestId, new ModelSelectionPolicy([request.Model]), _requirements, snapshot),
            cancellationToken).ConfigureAwait(false);
        if (selection is not ModelSelected selected)
        {
            return new ModelJudgeFailed(ModelJudgeFailureKind.NotConfigured, "No catalog model satisfies the explicit judge model alias.");
        }

        var model = selected.Decision.Model;
        if (_resolver.Resolve(model) is not { } adapter)
        {
            return new ModelJudgeFailed(ModelJudgeFailureKind.NotConfigured, "No model adapter is registered for the judge model.");
        }

        var now = _timeProvider.GetUtcNow();
        var branch = finished.PreviousCursor.BranchId;
        ImmutableArray<AgentMessage> messages =
        [
            new SystemMessage(
                new MessageId(Guid.NewGuid()), finished.AgentId, finished.SessionId, finished.ConversationId, branch, null, null, now,
                MessageState.Complete, [new TextPart(request.Instructions, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty),
            new UserMessage(
                new MessageId(Guid.NewGuid()), finished.AgentId, finished.SessionId, finished.ConversationId, branch, finished.RunId, null, now,
                MessageState.Complete, [new TextPart(request.Candidate, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty),
        ];
        var settings = LlmRequestSettings.Default with
        {
            Temperature = _options.Temperature,
            MaxOutputTokens = _options.MaximumOutputTokens,
        };
        var result = await adapter.ExecuteAsync(
            new LlmModelRequest(
                new LlmRequestContext(modelRequestId, model, messages, [], LlmToolChoice.None, settings, ExtensionData.Empty),
                attempt: 1,
                now + _options.RequestTimeout,
                ProviderRequestOptions.Empty),
            NoOpJudgeObserver.Instance,
            cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case ModelAttemptCancelled when cancellationToken.IsCancellationRequested:
                throw new OperationCanceledException(cancellationToken);
            case ModelAttemptCompleted completed:
                var response = completed.Response;
                return response.StopReason != NormalizedStopReason.Completed || response.Parts.Any(static part => part is ToolCallPart)
                    ? new ModelJudgeFailed(ModelJudgeFailureKind.Incomplete, "The judge model did not complete a plain reply.")
                    : new ModelJudgeCompleted(
                        string.Concat(response.Parts.OfType<TextPart>().Select(static part => part.Text)),
                        response.Identity.ProviderId.Value ?? "unknown",
                        response.Identity.ResolvedModelId.Value ?? "unknown",
                        response.Usage.InputTokens,
                        response.Usage.OutputTokens);
            default:
                return new ModelJudgeFailed(ModelJudgeFailureKind.Unavailable, "The judge model request did not complete.");
        }
    }
}
