// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The token envelope one assembly request allocates within.</summary>
public sealed record ContextBudget
{
    /// <summary>Initializes a context budget envelope.</summary>
    /// <param name="maxContextTokens">The selected model's maximum context window in tokens.</param>
    /// <param name="reservedOutputTokens">The minimum output capacity reserved before selecting content.</param>
    /// <param name="providerOverheadTokens">The estimated provider framing and schema overhead.</param>
    /// <param name="estimationSafetyMargin">The fractional margin applied to guard tokenizer estimation error.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxContextTokens"/> is not positive, a reservation is negative, or
    /// <paramref name="estimationSafetyMargin"/> is negative or greater than one.
    /// </exception>
    public ContextBudget(
        long maxContextTokens,
        int reservedOutputTokens,
        int providerOverheadTokens,
        double estimationSafetyMargin)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxContextTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(reservedOutputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(providerOverheadTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(estimationSafetyMargin);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(estimationSafetyMargin, 1.0);

        MaxContextTokens = maxContextTokens;
        ReservedOutputTokens = reservedOutputTokens;
        ProviderOverheadTokens = providerOverheadTokens;
        EstimationSafetyMargin = estimationSafetyMargin;
    }

    /// <summary>Gets the selected model's maximum context window in tokens.</summary>
    public long MaxContextTokens { get; }

    /// <summary>Gets the minimum output capacity reserved before selecting content.</summary>
    public int ReservedOutputTokens { get; }

    /// <summary>Gets the estimated provider framing and schema overhead.</summary>
    public int ProviderOverheadTokens { get; }

    /// <summary>Gets the fractional margin applied to guard tokenizer estimation error.</summary>
    public double EstimationSafetyMargin { get; }
}
