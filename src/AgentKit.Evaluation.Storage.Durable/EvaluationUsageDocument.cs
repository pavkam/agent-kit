// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Storage;

/// <summary>Is the persisted form of an <see cref="EvaluationUsageSummary"/>.</summary>
/// <param name="ModelRequests">The number of model requests.</param>
/// <param name="InputTokens">The total input tokens, or <see langword="null"/> when unknown.</param>
/// <param name="OutputTokens">The total output tokens, or <see langword="null"/> when unknown.</param>
internal sealed record EvaluationUsageDocument(int ModelRequests, long? InputTokens, long? OutputTokens)
{
    /// <summary>Converts a usage summary to its persisted form.</summary>
    /// <param name="value">The non-null summary.</param>
    /// <returns>The document.</returns>
    internal static EvaluationUsageDocument FromDomain(EvaluationUsageSummary value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.ModelRequests, value.InputTokens, value.OutputTokens);
    }

    /// <summary>Restores the summary, re-running its validation.</summary>
    /// <returns>The summary.</returns>
    internal EvaluationUsageSummary ToDomain() => new(ModelRequests, InputTokens, OutputTokens);
}
