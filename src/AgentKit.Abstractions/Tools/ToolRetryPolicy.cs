// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how many times and how patiently one tool call's attempts are retried.</summary>
/// <remarks>
/// <para>
/// The attempt budget counts the first attempt: <see cref="MaximumAttempts"/> of one means no retry. The policy carries
/// only pacing; whether a failed attempt is <em>eligible</em> for retry is decided by the executor from the outcome's
/// retryability, the tool's declared effect and idempotency, the side-effect certainty, and whether the invoker
/// enforces the declared idempotency mechanism. A policy can therefore never make an unsafe retry safe.
/// </para>
/// <para>Backoff is deterministic given a caller-supplied unit random value, so tests replay it exactly.</para>
/// </remarks>
public sealed record ToolRetryPolicy
{
    /// <summary>Initializes a validated retry policy.</summary>
    /// <param name="maximumAttempts">The positive total attempt budget including the first attempt.</param>
    /// <param name="initialDelay">The nonnegative delay before the second attempt, before jitter.</param>
    /// <param name="backoffMultiplier">The finite multiplier, at least one, applied for each later attempt.</param>
    /// <param name="maximumDelay">The upper bound for any computed delay; not less than <paramref name="initialDelay"/>.</param>
    /// <param name="jitterFraction">The fraction in [0, 1] of each delay that jitter may remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">A numeric argument is outside its documented range or not finite.</exception>
    public ToolRetryPolicy(
        int maximumAttempts,
        TimeSpan initialDelay,
        double backoffMultiplier,
        TimeSpan maximumDelay,
        double jitterFraction)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAttempts);
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDelay, initialDelay);
        ArgumentOutOfRangeException.ThrowIfNotEqual(double.IsFinite(backoffMultiplier), true, nameof(backoffMultiplier));
        ArgumentOutOfRangeException.ThrowIfLessThan(backoffMultiplier, 1.0);
        ArgumentOutOfRangeException.ThrowIfNotEqual(double.IsFinite(jitterFraction), true, nameof(jitterFraction));
        ArgumentOutOfRangeException.ThrowIfLessThan(jitterFraction, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jitterFraction, 1.0);
        MaximumAttempts = maximumAttempts;
        InitialDelay = initialDelay;
        BackoffMultiplier = backoffMultiplier;
        MaximumDelay = maximumDelay;
        JitterFraction = jitterFraction;
    }

    /// <summary>Gets a policy that performs exactly one attempt.</summary>
    /// <value>An immutable single-attempt policy with no delay.</value>
    public static ToolRetryPolicy NoRetry { get; } = new(1, TimeSpan.Zero, 1.0, TimeSpan.Zero, 0.0);

    /// <summary>Gets the total attempt budget including the first attempt.</summary>
    /// <value>A positive count; one means the call is never retried.</value>
    public int MaximumAttempts { get; }

    /// <summary>Gets the delay before the second attempt, before jitter.</summary>
    public TimeSpan InitialDelay { get; }

    /// <summary>Gets the multiplier applied to the delay for each later attempt.</summary>
    public double BackoffMultiplier { get; }

    /// <summary>Gets the upper bound for any computed delay.</summary>
    public TimeSpan MaximumDelay { get; }

    /// <summary>Gets the fraction of each delay that jitter may remove.</summary>
    /// <value>A value in [0, 1]; zero is fully deterministic.</value>
    public double JitterFraction { get; }

    /// <summary>Computes the bounded delay before the attempt that follows <paramref name="failedAttempt"/>.</summary>
    /// <param name="failedAttempt">The positive attempt that just failed, counting the first attempt as one.</param>
    /// <param name="unitRandom">A random value in [0, 1) used only to apply jitter; pass zero for no jitter.</param>
    /// <returns>A delay in [0, <see cref="MaximumDelay"/>].</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="failedAttempt"/> is not positive or <paramref name="unitRandom"/> is outside [0, 1].</exception>
    public TimeSpan ComputeDelay(int failedAttempt, double unitRandom)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failedAttempt);
        ArgumentOutOfRangeException.ThrowIfNotEqual(double.IsFinite(unitRandom), true, nameof(unitRandom));
        ArgumentOutOfRangeException.ThrowIfLessThan(unitRandom, 0.0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(unitRandom, 1.0);
        var scaled = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, failedAttempt - 1);
        var bounded = Math.Min(scaled, MaximumDelay.TotalMilliseconds);
        var jittered = bounded * (1.0 - (JitterFraction * unitRandom));
        return TimeSpan.FromMilliseconds(jittered);
    }
}
