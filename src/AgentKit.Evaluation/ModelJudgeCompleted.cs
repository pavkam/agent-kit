// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Carries a judge model reply together with the exact provider and model that produced it.</summary>
public sealed record ModelJudgeCompleted: ModelJudgeResponse
{
    /// <summary>Initializes a validated reply.</summary>
    /// <param name="text">The non-null reply text; it is parsed and never persisted.</param>
    /// <param name="provider">The non-blank provider identity that served the request.</param>
    /// <param name="resolvedModel">The non-blank model identity the provider resolved.</param>
    /// <param name="inputTokens">The reported input tokens, or <see langword="null"/> when unknown.</param>
    /// <param name="outputTokens">The reported output tokens, or <see langword="null"/> when unknown.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="provider"/> or <paramref name="resolvedModel"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A token count is negative.</exception>
    public ModelJudgeCompleted(string text, string provider, string resolvedModel, long? inputTokens, long? outputTokens)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedModel);
        if (inputTokens is { } input)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(input, nameof(inputTokens));
        }

        if (outputTokens is { } output)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(output, nameof(outputTokens));
        }

        Text = text;
        Provider = provider;
        ResolvedModel = resolvedModel;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    /// <summary>Gets the reply text.</summary>
    public string Text { get; }

    /// <summary>Gets the provider identity that served the request.</summary>
    public string Provider { get; }

    /// <summary>Gets the model identity the provider resolved.</summary>
    public string ResolvedModel { get; }

    /// <summary>Gets the reported input tokens, or <see langword="null"/>.</summary>
    public long? InputTokens { get; }

    /// <summary>Gets the reported output tokens, or <see langword="null"/>.</summary>
    public long? OutputTokens { get; }
}
