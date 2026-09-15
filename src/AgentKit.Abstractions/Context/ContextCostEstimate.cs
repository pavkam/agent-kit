// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Estimates the bounded encoded size and optional token cost of one context candidate.</summary>
public sealed record ContextCostEstimate
{
    /// <summary>Creates an immutable estimate.</summary>
    /// <param name="utf8Bytes">The nonnegative UTF-8 byte count.</param>
    /// <param name="estimatedTokens">The optional nonnegative token estimate; null means unknown.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either supplied count is negative.</exception>
    public ContextCostEstimate(long utf8Bytes, int? estimatedTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(utf8Bytes);
        if (estimatedTokens is { } tokens)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tokens, nameof(estimatedTokens));
        }

        Utf8Bytes = utf8Bytes;
        EstimatedTokens = estimatedTokens;
    }

    /// <summary>Gets the estimated UTF-8 encoded byte count.</summary>
    /// <value>A nonnegative count.</value>
    public long Utf8Bytes { get; }

    /// <summary>Gets the provider-neutral token estimate when one is available.</summary>
    /// <value>A nonnegative count, or null when unknown.</value>
    public int? EstimatedTokens { get; }
}
