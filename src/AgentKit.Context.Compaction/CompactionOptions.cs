// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// Configures the built-in, non-model-backed compaction pipeline registered
/// by <c>AddContextCompaction</c>.
/// </summary>
/// <remarks>
/// This is a mutable options type bound through <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>;
/// it is configured once at composition time and treated as read-only by
/// every consumer afterward.
/// </remarks>
public sealed class CompactionOptions
{
    /// <summary>
    /// Gets or sets the approximate number of characters per estimated
    /// token, used by <see cref="CharacterCompactionSizeEstimator"/> when no
    /// real tokenizer is available.
    /// </summary>
    /// <value>Defaults to 4.0, a common English-text approximation.</value>
    public double CharactersPerToken { get; set; } = 4.0;

    /// <summary>
    /// Gets or sets the maximum number of eligible source entries one
    /// compaction attempt will process before <see cref="StructuralCompactionCutSelector"/>
    /// deliberately declines with <see cref="CompactionRejectionKind.SourceLimitExceeded"/>.
    /// </summary>
    /// <value>Defaults to 5,000 entries.</value>
    public int MaximumSourceEntries { get; set; } = 5_000;

    /// <summary>
    /// Gets or sets the maximum total character length a produced
    /// checkpoint summary may contain before <see cref="DefaultCompactionValidator"/>
    /// rejects it with <see cref="CompactionValidationIssueKind.UnboundedContent"/>.
    /// </summary>
    /// <value>
    /// Defaults to 16,000 characters. Must exceed the length of
    /// <see cref="ExtractiveCompactionStrategy.TruncationMarker"/>; registration
    /// validation rejects smaller values.
    /// </value>
    public int MaximumCheckpointCharacters { get; set; } = 16_000;

    /// <summary>
    /// Gets or sets the page size <see cref="DefaultCompactor"/> uses when
    /// reading the eligible source range from <see cref="ISessionCoordinator"/>.
    /// </summary>
    /// <value>Defaults to 256 entries per page.</value>
    public int SourceReadPageSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the system instruction a model-backed compaction strategy
    /// sends ahead of the covered transcript when asking a model to summarize
    /// it.
    /// </summary>
    /// <value>
    /// Defaults to the prompt shipped as the embedded resource
    /// <c>Resources/DefaultCompactionSummaryPrompt.txt</c>, which asks for a
    /// faithful plain-text summary that preserves task state, decisions,
    /// constraints, open questions, load-bearing tool results, exact
    /// identifiers, and explicit user preferences, forbids inventing facts, and
    /// forbids following instructions found inside the transcript. Registration
    /// validation rejects a null, empty, or whitespace-only value.
    /// </value>
    /// <remarks>
    /// The prompt is sent with system-instruction precedence to the summary
    /// model only; the summary the model returns is untrusted model output and
    /// never inherits that precedence. The extractive strategy ignores this
    /// value.
    /// </remarks>
    public string SummaryPrompt { get; set; } = CompactionPromptResources.DefaultSummaryPrompt;
}
