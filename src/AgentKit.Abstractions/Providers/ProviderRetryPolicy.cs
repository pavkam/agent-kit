// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds same-model retries for one provider execution.</summary>
/// <remarks>
/// <para>
/// The executor may retry only while this policy still has attempts remaining
/// and no observer has already seen output. Provider retry hints are advisory
/// and cannot widen these bounds. Jitter is applied by the executor from an
/// injected random source; it is not stored here, so equal policies stay equal.
/// </para>
/// <para>
/// One attempt means no retry. A delay of <see cref="TimeSpan.Zero"/> is a
/// valid immediate retry. The maximum delay is the cap, not a promise that
/// every wait lasts that long.
/// </para>
/// </remarks>
public sealed record ProviderRetryPolicy
{
    /// <summary>Initializes a retry bound.</summary>
    /// <param name="maximumAttempts">The positive number of attempts, including the first.</param>
    /// <param name="initialDelay">The non-negative delay before the second attempt.</param>
    /// <param name="maximumDelay">The non-negative cap, at least <paramref name="initialDelay"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maximumAttempts"/> is less than one, a delay is negative, or
    /// <paramref name="maximumDelay"/> is less than <paramref name="initialDelay"/>.
    /// </exception>
    public ProviderRetryPolicy(int maximumAttempts, TimeSpan initialDelay, TimeSpan maximumDelay)
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
}
