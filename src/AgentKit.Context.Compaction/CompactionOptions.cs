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

    /// <summary>
    /// Gets or sets the maximum number of transcript characters
    /// <see cref="ModelCompactionStrategy"/> sends to the summary model in one
    /// request.
    /// </summary>
    /// <value>
    /// Defaults to 120,000 characters (roughly 30,000 tokens at the default
    /// <see cref="CharactersPerToken"/>). A longer transcript is bounded by
    /// keeping its head and tail around
    /// <see cref="ModelCompactionStrategy.TruncationMarker"/>, and the
    /// truncation is recorded in the produced provenance. Must exceed the
    /// marker's length; registration validation rejects smaller values.
    /// </value>
    /// <remarks>
    /// This is a hard engine ceiling on provider input, independent of the
    /// selected model's context window. The extractive strategy ignores this
    /// value.
    /// </remarks>
    public int MaximumSummaryInputCharacters { get; set; } = 120_000;

    /// <summary>
    /// Gets or sets the selection policy naming the catalog alias(es)
    /// <see cref="ModelCompactionStrategy"/> may use to generate a summary.
    /// </summary>
    /// <value>
    /// Defaults to <see langword="null"/>, which is valid for the extractive
    /// pipeline but is rejected by <c>AddModelBackedContextCompaction</c>
    /// validation, because a summary model is an external fact the
    /// application must name explicitly rather than a default the framework
    /// may fabricate.
    /// </value>
    /// <remarks>
    /// The policy is resolved through the engine's registered
    /// <see cref="IModelCatalog"/>, <see cref="IModelSelector"/>, and
    /// <see cref="ILlmModelResolver"/>, exactly as the agent loop resolves a
    /// run's model, and is scoped to the compaction operation's captured
    /// authorization. The selected model must support system instructions so
    /// <see cref="SummaryPrompt"/> can be delivered with instruction precedence.
    /// </remarks>
    public ModelSelectionPolicy? SummaryModelPolicy { get; set; }
}
