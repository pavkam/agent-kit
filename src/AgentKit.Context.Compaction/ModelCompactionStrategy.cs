// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using System.Text.Json;

using Microsoft.Extensions.Options;

/// <summary>
/// The first-party model-backed <see cref="ICompactionStrategy"/>: renders the
/// covered entries into one bounded plain-text transcript, asks a selected
/// conversational model to summarize it under
/// <see cref="CompactionOptions.SummaryPrompt"/>, and returns the model's text
/// as the checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// Model selection mirrors the agent loop exactly: the strategy reads one
/// <see cref="IModelCatalog"/> snapshot, asks the engine's
/// <see cref="IModelSelector"/> to apply <see cref="CompactionOptions.SummaryModelPolicy"/>
/// within the compaction operation's captured authorization scope, and resolves
/// the chosen descriptor to its <see cref="ILlmModel"/> through
/// <see cref="ILlmModelResolver"/>. It does not open a parallel provider path,
/// select a provider by registration order, or fabricate a default model: a
/// missing or incompatible policy is reported as a typed
/// <see cref="CompactionStrategyUnsupported"/> before any provider I/O.
/// </para>
/// <para>
/// Exactly one non-streaming request is sent per attempt. The prompt travels
/// as a <see cref="SystemMessage"/> and the transcript as a single
/// <see cref="UserMessage"/> with no tools offered, the request deadline is
/// the compaction request's <see cref="CompactionRequest.Deadline"/>, and the
/// caller's cancellation token governs the attempt. The transcript is bounded
/// to <see cref="CompactionOptions.MaximumSummaryInputCharacters"/> and the
/// returned summary to <see cref="CompactionOptions.MaximumCheckpointCharacters"/>,
/// both by keeping head and tail around <see cref="TruncationMarker"/>; each
/// truncation is recorded under <see cref="ModelCompactionProvenanceKeys"/>.
/// </para>
/// <para>
/// The produced summary is untrusted model output. It is returned as a plain
/// <see cref="TextPart"/> inside a <see cref="CompactionCheckpoint"/> and is
/// never given system or developer precedence; only structural validation
/// stands between it and activation, and that validation does not prove the
/// prose factually entailed by its sources. System and developer messages
/// found in covered history are omitted from the transcript because stored
/// history never carries instruction authority. A response the provider
/// stopped for length, a response that requests a tool, and a response
/// without text are reported as non-retryable failures rather than partial
/// summaries. This strategy is not deterministic and records
/// <see cref="CompactionProducer.Deterministic"/> as <see langword="false"/>.
/// </para>
/// <para>
/// No prompt, transcript, or summary text enters logs, activity tags, or
/// provenance; only sizes, truncation flags, identities, and normalized
/// outcomes are recorded. The strategy is stateless and safe for concurrent
/// use across independent requests.
/// </para>
/// </remarks>
public sealed class ModelCompactionStrategy: ICompactionStrategy
{
    /// <summary>The strategy key this implementation records as provenance.</summary>
    public static readonly CompactionStrategyKey StrategyKey = new("agentkit.model-summary.v1");

    /// <summary>
    /// The marker inserted between the retained head and tail when the transcript exceeds
    /// <see cref="CompactionOptions.MaximumSummaryInputCharacters"/> or the summary exceeds
    /// <see cref="CompactionOptions.MaximumCheckpointCharacters"/>.
    /// </summary>
    /// <remarks>
    /// This is the same marker the extractive strategy uses, so the composition-time ceiling validation shared by
    /// both strategies guarantees room remains for retained text.
    /// </remarks>
    public const string TruncationMarker = ExtractiveCompactionStrategy.TruncationMarker;

    private const string _producerKindValue = "model-backed";

    private static readonly ModelRequirements _requirements = new() { RequiresSystemInstructions = true };

    private readonly IModelCatalog _modelCatalog;
    private readonly IModelSelector _modelSelector;
    private readonly ILlmModelResolver _llmModelResolver;
    private readonly ICompactionSizeEstimator _estimator;
    private readonly IIdentifierGenerator<ModelRequestId> _modelRequestIds;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly TimeProvider _timeProvider;
    private readonly string _summaryPrompt;
    private readonly ModelSelectionPolicy _summaryModelPolicy;
    private readonly int _maximumSummaryInputCharacters;
    private readonly int _maximumCheckpointCharacters;
    private readonly ILogger<ModelCompactionStrategy> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModelCompactionStrategy"/> class.</summary>
    /// <param name="modelCatalog">The engine-wide catalog whose snapshot the selector chooses from.</param>
    /// <param name="modelSelector">The selector that applies the summary model policy to the catalog.</param>
    /// <param name="llmModelResolver">The resolver that maps the selected descriptor to its executable adapter.</param>
    /// <param name="estimator">The size estimator used to report the produced checkpoint's advisory size.</param>
    /// <param name="modelRequestIds">Generates the identity of the single summary attempt.</param>
    /// <param name="messageIds">Generates identities for the prompt and transcript messages.</param>
    /// <param name="timeProvider">The clock used to timestamp the prompt and transcript messages.</param>
    /// <param name="options">
    /// The validated compaction options carrying the summary prompt, summary model policy, transcript ceiling, and
    /// checkpoint ceiling.
    /// </param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> carries a null, empty, or whitespace <see cref="CompactionOptions.SummaryPrompt"/>
    /// or a null <see cref="CompactionOptions.SummaryModelPolicy"/>; a model-backed strategy cannot run without both.
    /// </exception>
    public ModelCompactionStrategy(
        IModelCatalog modelCatalog,
        IModelSelector modelSelector,
        ILlmModelResolver llmModelResolver,
        ICompactionSizeEstimator estimator,
        IIdentifierGenerator<ModelRequestId> modelRequestIds,
        IIdentifierGenerator<MessageId> messageIds,
        TimeProvider timeProvider,
        IOptions<CompactionOptions> options,
        ILogger<ModelCompactionStrategy>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(modelCatalog);
        ArgumentNullException.ThrowIfNull(modelSelector);
        ArgumentNullException.ThrowIfNull(llmModelResolver);
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(modelRequestIds);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        if (string.IsNullOrWhiteSpace(value.SummaryPrompt))
        {
            throw new ArgumentException(
                $"{nameof(CompactionOptions.SummaryPrompt)} must not be null, empty, or whitespace.", nameof(options));
        }

        if (value.SummaryModelPolicy is not { } summaryModelPolicy)
        {
            throw new ArgumentException(
                $"{nameof(CompactionOptions.SummaryModelPolicy)} must name at least one candidate alias for model-backed compaction.",
                nameof(options));
        }

        _modelCatalog = modelCatalog;
        _modelSelector = modelSelector;
        _llmModelResolver = llmModelResolver;
        _estimator = estimator;
        _modelRequestIds = modelRequestIds;
        _messageIds = messageIds;
        _timeProvider = timeProvider;
        _summaryPrompt = value.SummaryPrompt;
        _summaryModelPolicy = summaryModelPolicy;
        _maximumSummaryInputCharacters = value.MaximumSummaryInputCharacters;
        _maximumCheckpointCharacters = value.MaximumCheckpointCharacters;
        _logger = logger ?? NullLogger<ModelCompactionStrategy>.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="CompactionStrategyUnsupported"/> when the cut covers no entries or no executable summary
    /// model can be selected, <see cref="CompactionStrategyFailed"/> when the provider attempt fails or returns a
    /// response that cannot stand as a summary, and <see cref="CompactionCheckpointProduced"/> otherwise. Caller
    /// cancellation observed during the attempt propagates as <see cref="OperationCanceledException"/> so the
    /// compactor can report a truthful not-attempted commit state.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var context = request.Request.Context;
        var coveredIds = request.Cut.CoveredEntryIds.ToImmutableHashSet();
        var coveredEntries = request.Source.Entries.Where(e => coveredIds.Contains(e.Id)).ToImmutableArray();

        if (coveredEntries.IsEmpty)
        {
            return new CompactionStrategyUnsupported(
                new CompactionRejection(
                    CompactionRejectionKind.NoSafeCut,
                    "The selected cut covers no entries.",
                    ExtensionData.Empty));
        }

        var transcript = RenderTranscript(coveredEntries);
        transcript = CompactionTextTruncation.KeepHeadAndTail(
            transcript, _maximumSummaryInputCharacters, TruncationMarker, out var inputTruncated);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            transcript = "(no extractable text in covered entries)";
        }

        var modelRequestId = _modelRequestIds.Create();
        var (model, adapter, unsupported) = await SelectModelAsync(context, modelRequestId, cancellationToken).ConfigureAwait(false);
        if (unsupported is not null)
        {
            return unsupported;
        }

        Debug.Assert(model is not null && adapter is not null, "A selection that is not unsupported yields both a descriptor and an adapter.");

        var llmRequest = new LlmModelRequest(
            new LlmRequestContext(
                modelRequestId,
                model,
                BuildMessages(request.Request, transcript),
                [],
                LlmToolChoice.None,
                LlmRequestSettings.Default,
                ExtensionData.Empty),
            attempt: 1,
            request.Request.Deadline,
            ProviderRequestOptions.Empty);

        CompactionLog.SummaryModelRequestStarted(
            _logger, context.CompactionId, context.SessionId, modelRequestId, model.Alias, transcript.Length, inputTruncated);

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

        switch (attemptResult)
        {
            case ModelAttemptCancelled when cancellationToken.IsCancellationRequested:
                CompactionLog.SummaryModelRequestCancelled(_logger, context.CompactionId, context.SessionId, modelRequestId);
                throw new OperationCanceledException("The summary model attempt was cancelled.", cancellationToken);

            case ModelAttemptCancelled:
                return Failed(context, modelRequestId, "the provider cancelled the attempt before it completed", retryable: false);

            case ModelAttemptFailed failed:
                return Failed(
                    context,
                    modelRequestId,
                    $"the provider attempt failed with {failed.Failure.Kind}",
                    retryable: failed.Failure.Kind is ProviderFailureKind.Throttling
                        or ProviderFailureKind.Unavailable
                        or ProviderFailureKind.Timeout);

            case ModelAttemptCompleted completed:
                return Complete(context, model, modelRequestId, completed.Response, transcript.Length, inputTruncated);

            default:
                throw new InvalidOperationException($"Unrecognized {nameof(ModelAttemptResult)} kind '{attemptResult.GetType()}'.");
        }
    }

    /// <summary>
    /// Selects and resolves the summary model the way the agent loop resolves a run's model: one catalog snapshot,
    /// one selector decision scoped to the compaction operation's captured authorization, one adapter resolution.
    /// </summary>
    private async Task<(ModelDescriptor? Model, ILlmModel? Adapter, CompactionStrategyUnsupported? Unsupported)> SelectModelAsync(
        CompactionOperationContext context, ModelRequestId modelRequestId, CancellationToken cancellationToken)
    {
        Debug.Assert(context is not null, "The request validated its context.");

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
                var descriptor = selected.Decision.Model;
                var adapter = _llmModelResolver.Resolve(descriptor);
                if (adapter is null)
                {
                    CompactionLog.SummaryModelSelectionFailed(
                        _logger, context.CompactionId, context.SessionId, "no adapter registered for the selected model");
                    return (null, null, Unsupported(
                        $"Model alias '{descriptor.Alias}' is configured in the catalog but no LLM model adapter is registered to execute it."));
                }

                return (descriptor, adapter, null);

            default:
                throw new InvalidOperationException($"Unrecognized {nameof(ModelSelectionResult)} kind '{selection.GetType()}'.");
        }

        static CompactionStrategyUnsupported Unsupported(string safeMessage) => new(
            new CompactionRejection(CompactionRejectionKind.PolicyViolation, safeMessage, ExtensionData.Empty));
    }

    /// <summary>
    /// Builds the two-message request: the configured prompt with system precedence and the transcript as the sole
    /// user turn, both stamped with the compaction operation's identities so provider diagnostics correlate.
    /// </summary>
    private ImmutableArray<AgentMessage> BuildMessages(CompactionRequest request, string transcript)
    {
        Debug.Assert(request is not null, "The strategy request validated its compaction request.");
        Debug.Assert(!string.IsNullOrWhiteSpace(transcript), "The caller substitutes a placeholder for an empty transcript.");

        var context = request.Context;
        var runId = context.Correlation is InRunOperationCorrelation inRun ? inRun.RunId : (RunId?) null;
        var turnId = context.Correlation is InRunOperationCorrelation { TurnId: { } inRunTurn } ? inRunTurn : (TurnId?) null;
        var now = _timeProvider.GetUtcNow();

        return
        [
            new SystemMessage(
                _messageIds.Create(),
                context.AgentId,
                context.SessionId,
                conversationId: null,
                request.BranchId,
                runId,
                turnId,
                now,
                MessageState.Complete,
                [new TextPart(_summaryPrompt, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty),
            new UserMessage(
                _messageIds.Create(),
                context.AgentId,
                context.SessionId,
                conversationId: null,
                request.BranchId,
                runId,
                turnId,
                now,
                MessageState.Complete,
                [new TextPart(transcript, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty),
        ];
    }

    /// <summary>
    /// Turns a completed provider response into a checkpoint, rejecting responses that cannot stand as a summary:
    /// a length-limited stop, any tool request, or no text at all.
    /// </summary>
    private CompactionStrategyResult Complete(
        CompactionOperationContext context,
        ModelDescriptor model,
        ModelRequestId modelRequestId,
        ModelResponse response,
        int inputCharacters,
        bool inputTruncated)
    {
        Debug.Assert(context is not null && model is not null && response is not null, "The caller matched a completed attempt.");

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

        summary = CompactionTextTruncation.KeepHeadAndTail(summary, _maximumCheckpointCharacters, TruncationMarker, out var outputTruncated);
        CompactionLog.SummaryModelRequestCompleted(
            _logger, context.CompactionId, context.SessionId, modelRequestId, summary.Length, outputTruncated);

        var checkpoint = new CompactionCheckpoint(
            [new TextPart(summary, TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var producer = new CompactionProducer(
            StrategyKey,
            deterministic: false,
            Provenance(model, modelRequestId, response, inputCharacters, inputTruncated, outputTruncated));

        return new CompactionCheckpointProduced(checkpoint, producer, _estimator.EstimateCheckpoint(checkpoint));
    }

    private CompactionStrategyFailed Failed(
        CompactionOperationContext context, ModelRequestId modelRequestId, string reason, bool retryable)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(reason), "Callers supply a content-free reason.");

        CompactionLog.SummaryModelRequestFailed(_logger, context.CompactionId, context.SessionId, modelRequestId, reason);
        return new CompactionStrategyFailed(
            new CompactionFailure(
                CompactionFailureKind.StrategyFailure,
                $"The summary model attempt did not produce a summary: {reason}.",
                retryable,
                ExtensionData.Empty));
    }

    /// <summary>
    /// Renders covered entries as a role-labelled transcript. System and developer messages are omitted because
    /// stored history never carries instruction authority; entries without extractable text contribute nothing.
    /// </summary>
    private static string RenderTranscript(ImmutableArray<SessionEntry> coveredEntries)
    {
        Debug.Assert(!coveredEntries.IsDefaultOrEmpty, "The caller rejects an empty cut before rendering.");

        var builder = new StringBuilder();
        foreach (var entry in coveredEntries)
        {
            var role = entry switch
            {
                MessageSessionEntry { Message: UserMessage } => "user",
                MessageSessionEntry { Message: AssistantMessage } => "assistant",
                MessageSessionEntry { Message: ToolMessage } => "tool",
                MessageSessionEntry { Message: RuntimeMessage } => "runtime",
                MessageSessionEntry => null,
                CompactionSessionEntry => "earlier summary",
                _ => null,
            };

            if (role is null)
            {
                continue;
            }

            var text = ContentTextExtractor.ExtractEntryText(entry);
            if (text.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                _ = builder.Append("\n\n");
            }

            _ = builder.Append('[').Append(role).Append("]\n").Append(text);
        }

        return builder.ToString();
    }

    private static ExtensionData Provenance(
        ModelDescriptor model,
        ModelRequestId modelRequestId,
        ModelResponse response,
        int inputCharacters,
        bool inputTruncated,
        bool outputTruncated)
    {
        Debug.Assert(model is not null && response is not null, "The caller supplies the completed response.");

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
        values.Add(ModelCompactionProvenanceKeys.InputTruncated, Json(inputTruncated));
        values.Add(ModelCompactionProvenanceKeys.OutputTruncated, Json(outputTruncated));
        return new ExtensionData(values.ToImmutable());

        static ExtensionValue Json<T>(T value) => new([.. JsonSerializer.SerializeToUtf8Bytes(value)]);
    }
}
