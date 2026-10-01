// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Summarizes the model usage a case repetition reported, keeping unreported usage unknown instead of zero.</summary>
/// <remarks>Token totals are present only when every model request in the run reported final usage with that dimension; otherwise the dimension is <see langword="null"/>. A partial sum would be a wrong number presented as a measurement.</remarks>
public sealed record EvaluationUsageSummary
{
    /// <summary>Gets the summary of a run that made no model request or reported nothing.</summary>
    public static EvaluationUsageSummary None { get; } = new(0, null, null);

    /// <summary>Initializes a validated summary.</summary>
    /// <param name="modelRequests">The non-negative number of model requests the run made.</param>
    /// <param name="inputTokens">The total input tokens, or <see langword="null"/> when any request left them unknown.</param>
    /// <param name="outputTokens">The total output tokens, or <see langword="null"/> when any request left them unknown.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count or total is negative.</exception>
    public EvaluationUsageSummary(int modelRequests, long? inputTokens, long? outputTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(modelRequests);
        if (inputTokens is { } input)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(input, nameof(inputTokens));
        }

        if (outputTokens is { } output)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(output, nameof(outputTokens));
        }

        ModelRequests = modelRequests;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    /// <summary>Gets the number of model requests the run made.</summary>
    public int ModelRequests { get; }

    /// <summary>Gets the total input tokens, or <see langword="null"/> when unknown.</summary>
    public long? InputTokens { get; }

    /// <summary>Gets the total output tokens, or <see langword="null"/> when unknown.</summary>
    public long? OutputTokens { get; }
}
