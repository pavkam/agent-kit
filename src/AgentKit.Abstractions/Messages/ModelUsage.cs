// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;
using System.Numerics;

/// <summary>Retains portable usage evidence for one model response or attempt.</summary>
/// <remarks>Nullable fields distinguish unknown values from reported zero. Report lifecycle is independent of transport completion.</remarks>
public sealed record ModelUsage
{
    /// <summary>Gets the shared value representing absence of provider usage evidence.</summary>
    /// <value>A report with no counters, cost, currency, or extension evidence.</value>
    public static ModelUsage NotReported { get; } = new(
        ModelUsageReportState.NotReported, null, null, null, null, null, null, ExtensionData.Empty);

    /// <summary>Initializes validated immutable usage evidence.</summary>
    /// <param name="reportState">The provider report's usage lifecycle state.</param>
    /// <param name="inputTokens">The nonnegative input-token count, when reported.</param>
    /// <param name="outputTokens">The nonnegative output-token count, when reported.</param>
    /// <param name="cachedInputTokens">The nonnegative cached-input-token count, when reported.</param>
    /// <param name="reasoningTokens">The nonnegative reasoning-token count, when reported.</param>
    /// <param name="estimatedCost">The nonnegative estimated cost, when reported.</param>
    /// <param name="costCurrency">The nonblank currency identity, when reported independently of cost.</param>
    /// <param name="extensions">Provider-specific usage evidence retained without interpretation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reportState"/> is undefined, or a supplied numeric value is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="costCurrency"/> is blank, or a not-reported value carries usage evidence.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ModelUsage(
        ModelUsageReportState reportState,
        long? inputTokens,
        long? outputTokens,
        long? cachedInputTokens,
        long? reasoningTokens,
        decimal? estimatedCost,
        string? costCurrency,
        ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(reportState);
        ThrowIfNegative(inputTokens, nameof(inputTokens));
        ThrowIfNegative(outputTokens, nameof(outputTokens));
        ThrowIfNegative(cachedInputTokens, nameof(cachedInputTokens));
        ThrowIfNegative(reasoningTokens, nameof(reasoningTokens));
        ThrowIfNegative(estimatedCost, nameof(estimatedCost));
        if (costCurrency is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(costCurrency);
        }

        ArgumentNullException.ThrowIfNull(extensions);
        if (reportState == ModelUsageReportState.NotReported)
        {
            ArgumentException.ThrowIfNotEqual(inputTokens, null, nameof(inputTokens));
            ArgumentException.ThrowIfNotEqual(outputTokens, null, nameof(outputTokens));
            ArgumentException.ThrowIfNotEqual(cachedInputTokens, null, nameof(cachedInputTokens));
            ArgumentException.ThrowIfNotEqual(reasoningTokens, null, nameof(reasoningTokens));
            ArgumentException.ThrowIfNotEqual(estimatedCost, null, nameof(estimatedCost));
            ArgumentException.ThrowIfNotEqual(costCurrency, null, nameof(costCurrency));
            ArgumentException.ThrowIfNotEqual(extensions.Values.Count, 0, nameof(extensions));
        }

        ReportState = reportState;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CachedInputTokens = cachedInputTokens;
        ReasoningTokens = reasoningTokens;
        EstimatedCost = estimatedCost;
        CostCurrency = costCurrency;
        Extensions = extensions;
    }

    /// <summary>Gets the provider report's usage lifecycle state.</summary>
    /// <value>The explicit absence, interim, or final state assigned by the mapping boundary.</value>
    public ModelUsageReportState ReportState { get; }

    /// <summary>Gets the input-token count, or null when unknown.</summary>
    /// <value>A nonnegative count; null is distinct from reported zero.</value>
    public long? InputTokens { get; }

    /// <summary>Gets the output-token count, or null when unknown.</summary>
    /// <value>A nonnegative count; null is distinct from reported zero.</value>
    public long? OutputTokens { get; }

    /// <summary>Gets cached input tokens, typically included in rather than additional to <see cref="InputTokens"/>.</summary>
    /// <value>A nonnegative provider-reported count, or null when unknown.</value>
    public long? CachedInputTokens { get; }

    /// <summary>Gets hidden reasoning tokens separately from visible output tokens.</summary>
    /// <value>A nonnegative provider-reported count, or null when unknown.</value>
    public long? ReasoningTokens { get; }

    /// <summary>Gets provider-computed or locally estimated cost without implying billing finality.</summary>
    /// <value>A nonnegative amount, or null when no cost estimate is available.</value>
    public decimal? EstimatedCost { get; }

    /// <summary>Gets the reported currency identity independently of whether cost is known.</summary>
    /// <value>A nonblank opaque currency identity, or null when unknown.</value>
    public string? CostCurrency { get; }

    /// <summary>Gets immutable provider-specific usage evidence not represented by portable counters.</summary>
    /// <value>An owned immutable extension bag; it is empty for <see cref="NotReported"/>.</value>
    public ExtensionData Extensions { get; }

    private static void ThrowIfNegative<T>(T? value, string paramName)
        where T : struct, INumber<T>
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(paramName), "A public parameter name is required.");
        if (value.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.Value, paramName);
        }
    }
}
