// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds same-model retries for one semantic operation execution.</summary>
public sealed record SemanticOperationRetryPolicy
{
    /// <summary>Initializes a retry bound.</summary>
    /// <param name="maximumAttempts">The positive number of attempts, including the first.</param>
    /// <param name="initialDelay">The non-negative delay before the second attempt.</param>
    /// <param name="maximumDelay">The non-negative cap, at least <paramref name="initialDelay"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is invalid.</exception>
    public SemanticOperationRetryPolicy(int maximumAttempts, TimeSpan initialDelay, TimeSpan maximumDelay)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAttempts);
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDelay, initialDelay);
        MaximumAttempts = maximumAttempts;
        InitialDelay = initialDelay;
        MaximumDelay = maximumDelay;
    }

    /// <summary>Gets the positive attempt limit, including the first attempt.</summary>
    public int MaximumAttempts { get; }

    /// <summary>Gets the non-negative delay before a second attempt.</summary>
    public TimeSpan InitialDelay { get; }

    /// <summary>Gets the non-negative delay cap.</summary>
    public TimeSpan MaximumDelay { get; }

    /// <summary>Gets a single-attempt policy with no delay.</summary>
    public static SemanticOperationRetryPolicy None { get; } = new(1, TimeSpan.Zero, TimeSpan.Zero);
}
