// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>Model-backed compaction strategy that delegates summary generation to a registered generator.</summary>
public sealed class ModelCompactionStrategy: ICompactionStrategy
{
    /// <summary>The strategy key this implementation records as provenance.</summary>
    public static readonly CompactionStrategyKey StrategyKey = new("agentkit.model-summary.v1");

    /// <summary>Truncation marker shared with the extractive strategy.</summary>
    public const string TruncationMarker = ExtractiveCompactionStrategy.TruncationMarker;

    private readonly ComponentKey<ICompactor> _compactorKey;
    private readonly ICompactionSummaryGeneratorResolver _generators;
    private readonly ICompactionSizeEstimator _estimator;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly TimeProvider _timeProvider;
    private readonly string _summaryPrompt;
    private readonly int _maximumSummaryInputCharacters;

    /// <summary>Initializes a new instance of the <see cref="ModelCompactionStrategy"/> class.</summary>
    public ModelCompactionStrategy(
        ComponentKey<ICompactor> compactorKey,
        ICompactionSummaryGeneratorResolver generators,
        ICompactionSizeEstimator estimator,
        IIdentifierGenerator<MessageId> messageIds,
        TimeProvider timeProvider,
        IOptions<CompactionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(generators);
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);
        if (string.IsNullOrWhiteSpace(options.Value.SummaryPrompt))
        {
            throw new ArgumentException(
                $"{nameof(CompactionOptions.SummaryPrompt)} must not be null, empty, or whitespace.", nameof(options));
        }

        _compactorKey = compactorKey;
        _generators = generators;
        _estimator = estimator;
        _messageIds = messageIds;
        _timeProvider = timeProvider;
        _summaryPrompt = options.Value.SummaryPrompt;
        _maximumSummaryInputCharacters = options.Value.MaximumSummaryInputCharacters;
    }

    /// <inheritdoc/>
    public CompactionStrategyDescriptor Descriptor { get; } = new(
        StrategyKey,
        new CompactionStrategyVersion("1"),
        CompactionStrategyCapabilities.SemanticSummary,
        deterministic: false,
        summaryGeneratorKey: ModelBackedSummaryGenerator.GeneratorKey);

    /// <inheritdoc/>
    public async Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

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

        var messages = BuildMessages(request.Request, transcript);
        var fingerprint = request.Request.EffectiveInstructionsFingerprint ?? new ContentHash("none");
        var summaryRequest = new CompactionSummaryRequest(
            request.Request.Context,
            ModelBackedSummaryGenerator.GeneratorKey,
            request.Cut.CoveredRange,
            [
                new CompactionSummarySegment(
                    request.Cut.CoveredEntryIds,
                    messages,
                    [],
                    new ContentHash($"segment:{transcript.Length}"))
            ],
            maximumOutputTokens: request.Request.TargetInputTokens > 0 ? request.Request.TargetInputTokens : 2048,
            fingerprint,
            request.Request.Deadline,
            ExtensionData.Empty);

        var resolution = await _generators
            .ResolveAsync(_compactorKey, ModelBackedSummaryGenerator.GeneratorKey, cancellationToken)
            .ConfigureAwait(false);
        if (resolution is CompactionSummaryGeneratorNotFound notFound)
        {
            return new CompactionStrategyUnsupported(
                new CompactionRejection(
                    CompactionRejectionKind.PolicyViolation,
                    $"No summary generator '{notFound.GeneratorKey}' is registered for compactor '{notFound.CompactorKey}'.",
                    ExtensionData.Empty));
        }

        var generator = ((CompactionSummaryGeneratorResolved) resolution).Generator;
        var generation = await generator.GenerateAsync(summaryRequest, budget, cancellationToken).ConfigureAwait(false);
        return generation switch
        {
            CompactionSummaryGenerationUnsupported unsupported => new CompactionStrategyUnsupported(unsupported.Rejection),
            CompactionSummaryGenerationFailed failed => new CompactionStrategyFailed(failed.Failure),
            CompactionSummaryGenerationCancelled cancelled => new CompactionStrategyCancelled(cancelled.Cancellation),
            CompactionSummaryGenerated generated => ToCheckpoint(generated.Summary, inputTruncated, transcript.Length),
            _ => throw new InvalidOperationException($"Unrecognized {nameof(CompactionSummaryGenerationResult)}.")
        };
    }

    private CompactionCheckpointProduced ToCheckpoint(
        CompactionGeneratedSummary summary,
        bool inputTruncated,
        int inputCharacters)
    {
        var checkpoint = new CompactionCheckpoint(summary.Content, ExtensionData.Empty);
        var producerExtensions = MergeInputProvenance(summary.Extensions, inputTruncated, inputCharacters);
        var producer = new CompactionProducer(StrategyKey, deterministic: false, producerExtensions);
        return new CompactionCheckpointProduced(checkpoint, producer, _estimator.EstimateCheckpoint(checkpoint));
    }

    private static ExtensionData MergeInputProvenance(ExtensionData summaryExtensions, bool inputTruncated, int inputCharacters)
    {
        var values = ImmutableDictionary.CreateBuilder<string, ExtensionValue>(StringComparer.Ordinal);
        foreach (var (key, value) in summaryExtensions.Values)
        {
            values[key] = value;
        }

        values[ModelCompactionProvenanceKeys.InputCharacters] = JsonNumber(inputCharacters);
        values[ModelCompactionProvenanceKeys.InputTruncated] = JsonBoolean(inputTruncated);
        return new ExtensionData(values.ToImmutable());

        static ExtensionValue JsonNumber(int value) => new([.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value)]);
        static ExtensionValue JsonBoolean(bool value) => new([.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value)]);
    }

    private ImmutableArray<AgentMessage> BuildMessages(CompactionRequest request, string transcript)
    {
        var context = request.Context;
        var runId = context.Correlation is InRunOperationCorrelation inRun ? inRun.RunId : (RunId?) null;
        var turnId = context.Correlation is InRunOperationCorrelation { TurnId: { } inRunTurn } ? inRunTurn : (TurnId?) null;
        var now = _timeProvider.GetUtcNow();
        return
        [
            new SystemMessage(
                _messageIds.Create(), context.AgentId, context.SessionId, null, request.BranchId, runId, turnId, now,
                MessageState.Complete,
                [new TextPart(_summaryPrompt, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty),
            new UserMessage(
                _messageIds.Create(), context.AgentId, context.SessionId, null, request.BranchId, runId, turnId, now,
                MessageState.Complete,
                [new TextPart(transcript, TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty),
        ];
    }

    private static string RenderTranscript(ImmutableArray<SessionEntry> coveredEntries)
    {
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
}
