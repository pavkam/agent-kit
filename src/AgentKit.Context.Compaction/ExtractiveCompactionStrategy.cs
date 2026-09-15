// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.Options;

/// <summary>
/// The built-in <see cref="ICompactionStrategy"/>: produces a checkpoint by
/// concatenating each covered entry's extracted text, without invoking any
/// model.
/// </summary>
/// <remarks>
/// <para>
/// This strategy is deterministic given identical input: the same covered
/// entries always produce byte-identical checkpoint text, which is what
/// lets it stand in as a first-party default without requiring provider
/// access, budget reservation, or non-determinism handling. It never
/// paraphrases or summarizes semantically; it extracts and truncates, which
/// is why it is registered under the strategy key
/// <c>"agentkit.extractive.v1"</c> rather than a generic "summarizer" name.
/// </para>
/// <para>
/// When the concatenated extract exceeds the configured character ceiling,
/// this strategy truncates by keeping a leading and trailing portion and
/// replacing the middle with a marker, on the theory that the beginning
/// (initial context) and end (most recent developments) of a covered range
/// are typically more load-bearing than its middle for future turns.
/// </para>
/// </remarks>
public sealed class ExtractiveCompactionStrategy: ICompactionStrategy
{
    /// <summary>The strategy key this implementation records as provenance.</summary>
    public static readonly CompactionStrategyKey StrategyKey = new("agentkit.extractive.v1");

    /// <summary>
    /// The marker inserted between the retained head and tail when an extract exceeds
    /// <see cref="CompactionOptions.MaximumCheckpointCharacters"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ServiceExtensions.AddContextCompaction"/> requires the configured ceiling to exceed this marker's
    /// length; otherwise no room would remain for any extracted text and every attempt would fail validation.
    /// </remarks>
    public const string TruncationMarker = "\n...[truncated]...\n";

    private readonly ICompactionSizeEstimator _estimator;
    private readonly int _maximumCheckpointCharacters;

    /// <summary>Initializes a new instance of the <see cref="ExtractiveCompactionStrategy"/> class.</summary>
    /// <param name="estimator">The size estimator used to measure the produced checkpoint.</param>
    /// <param name="options">The validated compaction options carrying the checkpoint character ceiling.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="estimator"/> or <paramref name="options"/> is null.
    /// </exception>
    public ExtractiveCompactionStrategy(ICompactionSizeEstimator estimator, IOptions<CompactionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(options);

        _estimator = estimator;
        _maximumCheckpointCharacters = options.Value.MaximumCheckpointCharacters;
    }

    /// <inheritdoc/>
    public Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var coveredIds = request.Cut.CoveredEntryIds.ToImmutableHashSet();
        var coveredEntries = request.Source.Entries.Where(e => coveredIds.Contains(e.Id)).ToImmutableArray();

        if (coveredEntries.IsEmpty)
        {
            return Task.FromResult<CompactionStrategyResult>(new CompactionStrategyUnsupported(
                new CompactionRejection(
                    CompactionRejectionKind.NoSafeCut,
                    "The selected cut covers no entries.",
                    ExtensionData.Empty)));
        }

        var extract = string.Join(
            '\n', coveredEntries.Select(ContentTextExtractor.ExtractEntryText).Where(static t => t.Length > 0));

        extract = CompactionTextTruncation.KeepHeadAndTail(extract, _maximumCheckpointCharacters, TruncationMarker, out _);
        if (extract.Length == 0)
        {
            extract = "(no extractable text in covered entries)";
        }

        var summary = ImmutableArray.Create<ContentPart>(new TextPart(extract, TextSemantics.Plain, ExtensionData.Empty));
        var checkpoint = new CompactionCheckpoint(summary, ExtensionData.Empty);
        var producer = new CompactionProducer(StrategyKey, deterministic: true, ExtensionData.Empty);
        var after = _estimator.EstimateCheckpoint(checkpoint);

        return Task.FromResult<CompactionStrategyResult>(new CompactionCheckpointProduced(checkpoint, producer, after));
    }
}
