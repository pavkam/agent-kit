// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Portable token usage and cost accounting for one model response or
/// attempt. Provider-specific counters that do not fit these fields remain
/// in <see cref="Extensions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Every field is nullable rather than defaulting to zero, because "the
/// provider did not report this counter" and "the provider reported exactly
/// zero" are different, both-legitimate facts: budget accounting and cost
/// dashboards need to distinguish "we don't know" from "we know it was
/// free," and silently coercing an unreported value to zero would corrupt
/// both. <see cref="Empty"/> exists only for the narrow case of an attempt
/// that failed before the provider reported anything at all, and it is
/// never treated as equivalent to a genuine all-zero report.
/// </para>
/// </remarks>
public sealed record ModelUsage
{
    /// <summary>
    /// Gets a safe zero-usage default for attempts that fail before a
    /// provider reports any counters. It is never substituted for a real
    /// reported value once one is available, and budget/cost accounting
    /// must not treat it as evidence that the attempt actually cost
    /// nothing.
    /// </summary>
    public static ModelUsage Empty { get; } = new(
        inputTokens: null,
        outputTokens: null,
        cachedInputTokens: null,
        reasoningTokens: null,
        estimatedCost: null,
        costCurrency: null,
        extensions: ExtensionData.Empty);

    /// <summary>Initializes a new instance of the <see cref="ModelUsage"/> record.</summary>
    /// <param name="inputTokens">The number of input tokens consumed, when reported.</param>
    /// <param name="outputTokens">The number of output tokens produced, when reported.</param>
    /// <param name="cachedInputTokens">
    /// The number of input tokens served from a provider cache, when
    /// reported. This is typically a subset of, not additional to,
    /// <paramref name="inputTokens"/>.
    /// </param>
    /// <param name="reasoningTokens">
    /// The number of hidden reasoning tokens produced, when reported. These
    /// are usually billed but not returned as visible content.
    /// </param>
    /// <param name="estimatedCost">
    /// The provider-computed or locally estimated cost, when available.
    /// </param>
    /// <param name="costCurrency">
    /// The ISO 4217 currency code for <paramref name="estimatedCost"/>, when
    /// available.
    /// </param>
    /// <param name="extensions">Provider-specific usage data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ModelUsage(
        long? inputTokens,
        long? outputTokens,
        long? cachedInputTokens,
        long? reasoningTokens,
        decimal? estimatedCost,
        string? costCurrency,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CachedInputTokens = cachedInputTokens;
        ReasoningTokens = reasoningTokens;
        EstimatedCost = estimatedCost;
        CostCurrency = costCurrency;
        Extensions = extensions;
    }

    /// <summary>Gets the number of input tokens consumed, when reported.</summary>
    public long? InputTokens { get; init; }

    /// <summary>Gets the number of output tokens produced, when reported.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>
    /// Gets the number of input tokens served from a provider cache, when
    /// reported. This is typically a subset of, not additional to,
    /// <see cref="InputTokens"/>.
    /// </summary>
    public long? CachedInputTokens { get; init; }

    /// <summary>
    /// Gets the number of hidden reasoning tokens produced, when reported.
    /// These are usually billed but not returned as visible content.
    /// </summary>
    public long? ReasoningTokens { get; init; }

    /// <summary>
    /// Gets the provider-computed or locally estimated cost, when
    /// available.
    /// </summary>
    public decimal? EstimatedCost { get; init; }

    /// <summary>
    /// Gets the ISO 4217 currency code for <see cref="EstimatedCost"/>, when
    /// available.
    /// </summary>
    public string? CostCurrency { get; init; }

    /// <summary>Gets provider-specific usage data.</summary>
    public ExtensionData Extensions { get; init; }
}
