// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using System.Text.Json;

using Microsoft.Extensions.Options;

/// <summary>First-party model-backed <see cref="ICompactionSummaryGenerator"/>.</summary>
public sealed class ModelBackedSummaryGenerator: ICompactionSummaryGenerator
{
    /// <summary>The generator key for this implementation.</summary>
    public static readonly CompactionSummaryGeneratorKey GeneratorKey = new("agentkit.model-summary.v1");

    private const string _producerKindValue = "model-backed";

    private static readonly ModelRequirements _requirements = new() { RequiresSystemInstructions = true };

    private readonly IModelCatalog _modelCatalog;
    private readonly IModelSelector _modelSelector;
    private readonly ILlmModelResolver _llmModelResolver;
    private readonly IIdentifierGenerator<ModelRequestId> _modelRequestIds;
    private readonly ModelSelectionPolicy _summaryModelPolicy;
    private readonly int _maximumCheckpointCharacters;
    private readonly ILogger<ModelBackedSummaryGenerator> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModelBackedSummaryGenerator"/> class.</summary>
    public ModelBackedSummaryGenerator(
        IModelCatalog modelCatalog,
        IModelSelector modelSelector,
        ILlmModelResolver llmModelResolver,
        IIdentifierGenerator<ModelRequestId> modelRequestIds,
        IOptions<CompactionOptions> options,
        ILogger<ModelBackedSummaryGenerator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(modelCatalog);
        ArgumentNullException.ThrowIfNull(modelSelector);
        ArgumentNullException.ThrowIfNull(llmModelResolver);
        ArgumentNullException.ThrowIfNull(modelRequestIds);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Value.SummaryModelPolicy is not { } summaryModelPolicy)
        {
            throw new ArgumentException(
                $"{nameof(CompactionOptions.SummaryModelPolicy)} must name at least one candidate alias for model-backed compaction.",
                nameof(options));
        }

        _modelCatalog = modelCatalog;
        _modelSelector = modelSelector;
        _llmModelResolver = llmModelResolver;
        _modelRequestIds = modelRequestIds;
        _summaryModelPolicy = summaryModelPolicy;
        _maximumCheckpointCharacters = options.Value.MaximumCheckpointCharacters;
        _logger = logger ?? NullLogger<ModelBackedSummaryGenerator>.Instance;
    }

    /// <inheritdoc/>
    public CompactionSummaryGeneratorDescriptor Descriptor { get; } = new(
        GeneratorKey,
        new CompactionSummaryGeneratorVersion("1"),
        modelBacked: true,
        deterministic: false,
        maximumInputTokens: int.MaxValue,
        maximumOutputTokens: int.MaxValue);

    /// <inheritdoc/>
    public async Task<CompactionSummaryGenerationResult> GenerateAsync(
        CompactionSummaryRequest request,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = budget;
        cancellationToken.ThrowIfCancellationRequested();

        var context = request.Context;
        var segment = request.Segments[0];
        if (segment.Messages.IsEmpty)
        {
            return new CompactionSummaryGenerationUnsupported(
                new CompactionRejection(
                    CompactionRejectionKind.PolicyViolation,
                    "The summary request did not include any messages.",
                    ExtensionData.Empty));
        }

        var inputCharacters = segment.Messages
            .OfType<UserMessage>()
            .SelectMany(static message => message.Parts)
            .OfType<TextPart>()
            .Sum(static part => part.Text.Length);

        var modelRequestId = _modelRequestIds.Create();
        var (model, adapter, unsupported) = await SelectModelAsync(context, modelRequestId, cancellationToken).ConfigureAwait(false);
        if (unsupported is not null)
        {
            return unsupported;
        }

        Debug.Assert(model is not null && adapter is not null);

        var llmRequest = new LlmModelRequest(
            new LlmRequestContext(
                modelRequestId,
                model,
                segment.Messages,
                [],
                LlmToolChoice.None,
                LlmRequestSettings.Default,
                ExtensionData.Empty),
            attempt: 1,
            request.Deadline,
            ProviderRequestOptions.Empty);

        CompactionLog.SummaryModelRequestStarted(
            _logger, context.CompactionId, context.SessionId, modelRequestId, model.Alias, inputCharacters, inputTruncated: false);

        ModelAttemptResult attemptResult;
        using (var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.Chat,
            ActivityKind.Client,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.Chat },
                { AgentKitTagNames.CompactionId, context.CompactionId.ToString() },
                { AgentKitTagNames.AgentId, context.AgentId.ToString() },
                { AgentKitTagNames.SessionId, context.SessionId.ToString() },
                { AgentKitTagNames.OperationId, context.Correlation.OperationId.ToString() },
                { AgentKitTagNames.ModelRequestId, modelRequestId.ToString() },
                { AgentKitTagNames.RequestModel, model.ModelId.ToString() },
                { AgentKitTagNames.ProviderName, model.ProviderId.ToString() },
            }))
        {
            try
            {
                attemptResult = await adapter.ExecuteAsync(llmRequest, NoOpModelResponseObserver.Instance, cancellationToken)
                    .ConfigureAwait(false);
                if (attemptResult is ModelAttemptCompleted)
                {
                    activity.SetSuccessful("completed");
                }
                else
                {
                    activity.SetFailed(attemptResult.GetType().Name, attemptResult.GetType().Name);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                activity.SetFailed("cancelled", "cancellation");
                CompactionLog.SummaryModelRequestCancelled(_logger, context.CompactionId, context.SessionId, modelRequestId);
                throw;
            }
            catch (Exception exception)
            {
                activity.SetFailed("faulted", exception.GetType().FullName ?? exception.GetType().Name);
                throw;
            }
        }

        return attemptResult switch
        {
            ModelAttemptCancelled when cancellationToken.IsCancellationRequested => throw new OperationCanceledException(cancellationToken),
            ModelAttemptCancelled => Failed(context, modelRequestId, "the provider cancelled the attempt before it completed", retryable: false),
            ModelAttemptFailed failed => Failed(
                context,
                modelRequestId,
                $"the provider attempt failed with {failed.Failure.Kind}",
                retryable: failed.Failure.Kind is ProviderFailureKind.Throttling
                    or ProviderFailureKind.Unavailable
                    or ProviderFailureKind.Timeout),
            ModelAttemptCompleted completed => Complete(context, model, modelRequestId, completed.Response, inputCharacters),
            _ => throw new InvalidOperationException($"Unrecognized {nameof(ModelAttemptResult)} kind '{attemptResult.GetType()}'.")
        };
    }

    private async Task<(ModelDescriptor? Model, ILlmModel? Adapter, CompactionSummaryGenerationUnsupported? Unsupported)> SelectModelAsync(
        CompactionOperationContext context, ModelRequestId modelRequestId, CancellationToken cancellationToken)
    {
        var catalog = await _modelCatalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var turnId = context.Correlation is InRunOperationCorrelation { TurnId: { } inRunTurn } ? inRunTurn : (TurnId?) null;
        var selectionRequest = new ModelSelectionRequest(
            context.Authorization.Scope, modelRequestId, _summaryModelPolicy, _requirements, catalog, turnId);

        var selection = await _modelSelector.SelectAsync(selectionRequest, cancellationToken).ConfigureAwait(false);
        switch (selection)
        {
            case InvalidModelPolicy invalid:
                CompactionLog.SummaryModelSelectionFailed(_logger, context.CompactionId, context.SessionId, "invalid summary model policy");
                return (null, null, Unsupported($"The summary model policy is invalid: {invalid.Reason}"));
            case NoCompatibleModel:
                CompactionLog.SummaryModelSelectionFailed(_logger, context.CompactionId, context.SessionId, "no compatible model");
                return (null, null, Unsupported(
                    "No configured model satisfies the summary model policy and its system-instruction requirement."));
            case ModelSelected selected:
                return ResolveSelected(context, selected.Decision.Model);
            default:
                throw new InvalidOperationException($"Unrecognized {nameof(ModelSelectionResult)} kind '{selection.GetType()}'.");
        }
    }

    private (ModelDescriptor? Model, ILlmModel? Adapter, CompactionSummaryGenerationUnsupported? Unsupported) ResolveSelected(
        CompactionOperationContext context,
        ModelDescriptor descriptor)
    {
        var adapter = _llmModelResolver.Resolve(descriptor);
        if (adapter is null)
        {
            CompactionLog.SummaryModelSelectionFailed(
                _logger, context.CompactionId, context.SessionId, "no adapter registered for the selected model");
            return (null, null, Unsupported(
                $"Model alias '{descriptor.Alias}' is configured in the catalog but no LLM model adapter is registered to execute it."));
        }

        return (descriptor, adapter, null);
    }

    private static CompactionSummaryGenerationUnsupported Unsupported(string safeMessage) => new(
        new CompactionRejection(CompactionRejectionKind.PolicyViolation, safeMessage, ExtensionData.Empty));

    private CompactionSummaryGenerationResult Complete(
        CompactionOperationContext context,
        ModelDescriptor model,
        ModelRequestId modelRequestId,
        ModelResponse response,
        int inputCharacters)
    {
        if (response.StopReason == NormalizedStopReason.Length)
        {
            return Failed(context, modelRequestId, "the provider stopped the summary at its output length limit", retryable: false);
        }

        if (response.StopReason == NormalizedStopReason.ToolUse || response.Parts.Any(static part => part is ToolCallPart))
        {
            return Failed(context, modelRequestId, "the model requested a tool instead of producing a summary", retryable: false);
        }

        if (response.StopReason != NormalizedStopReason.Completed)
        {
            return Failed(context, modelRequestId, $"the provider reported stop reason {response.StopReason}", retryable: false);
        }

        var summary = ContentTextExtractor.ExtractPartsText(response.Parts);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return Failed(context, modelRequestId, "the model returned no summary text", retryable: false);
        }

        summary = CompactionTextTruncation.KeepHeadAndTail(
            summary, _maximumCheckpointCharacters, ModelCompactionStrategy.TruncationMarker, out var outputTruncated);
        CompactionLog.SummaryModelRequestCompleted(
            _logger, context.CompactionId, context.SessionId, modelRequestId, summary.Length, outputTruncated);

        return new CompactionSummaryGenerated(
            new CompactionGeneratedSummary(
                [new TextPart(summary, TextSemantics.Plain, ExtensionData.Empty)],
                [],
                response.Identity.ProviderId,
                response.Identity.ResolvedModelId,
                null,
                response.Identity.ResponseId,
                response.Usage,
                Provenance(model, modelRequestId, response, inputCharacters, outputTruncated)));
    }

    private CompactionSummaryGenerationFailed Failed(
        CompactionOperationContext context, ModelRequestId modelRequestId, string reason, bool retryable)
    {
        CompactionLog.SummaryModelRequestFailed(_logger, context.CompactionId, context.SessionId, modelRequestId, reason);
        return new CompactionSummaryGenerationFailed(
            new CompactionFailure(
                CompactionFailureKind.StrategyFailure,
                $"The summary model attempt did not produce a summary: {reason}.",
                retryable,
                ExtensionData.Empty));
    }

    private static ExtensionData Provenance(
        ModelDescriptor model,
        ModelRequestId modelRequestId,
        ModelResponse response,
        int inputCharacters,
        bool outputTruncated)
    {
        var values = ImmutableDictionary.CreateBuilder<string, ExtensionValue>(StringComparer.Ordinal);
        values.Add(ModelCompactionProvenanceKeys.ProducerKind, Json(_producerKindValue));
        values.Add(ModelCompactionProvenanceKeys.ModelAlias, Json(model.Alias.Value));
        values.Add(ModelCompactionProvenanceKeys.ProviderId, Json(response.Identity.ProviderId.Value));
        values.Add(ModelCompactionProvenanceKeys.ModelId, Json(response.Identity.ResolvedModelId.Value));
        values.Add(ModelCompactionProvenanceKeys.ModelRequestId, Json(modelRequestId.ToString()));
        if (response.Identity.ResponseId is { } responseId)
        {
            values.Add(ModelCompactionProvenanceKeys.ProviderResponseId, Json(responseId.Value));
        }

        if (response.Usage.InputTokens is { } inputTokens)
        {
            values.Add(ModelCompactionProvenanceKeys.InputTokens, Json(inputTokens));
        }

        if (response.Usage.OutputTokens is { } outputTokens)
        {
            values.Add(ModelCompactionProvenanceKeys.OutputTokens, Json(outputTokens));
        }

        values.Add(ModelCompactionProvenanceKeys.InputCharacters, Json(inputCharacters));
        values.Add(ModelCompactionProvenanceKeys.InputTruncated, Json(false));
        values.Add(ModelCompactionProvenanceKeys.OutputTruncated, Json(outputTruncated));
        return new ExtensionData(values.ToImmutable());

        static ExtensionValue Json<T>(T value) => new([.. JsonSerializer.SerializeToUtf8Bytes(value)]);
    }
}
