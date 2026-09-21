// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The bounded per-invocation overrides a caller may apply on top of an agent
/// definition's configured run policy.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// Session, conversation, execution identity, and input are separate,
/// explicit parameters on typed <see cref="Agent"/> run requests and the facade
/// <see cref="AgentRunRequest"/> rather than fields here: this type carries
/// only the bounded overrides, so the same immutable instance can accompany
/// any invocation regardless of who runs it or on which session.
/// </para>
/// <para>
/// Overrides are bounded and may only narrow the definition's limits. A run
/// cannot widen <see cref="RunPolicyDefaults.MaxTurns"/> or the attempt
/// timeout beyond what the definition allows, so an agent's configured
/// ceiling stays a ceiling.
/// </para>
/// </remarks>
public sealed record AgentRunOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRunOptions"/> record.
    /// </summary>
    /// <param name="maxTurns">
    /// An optional lower turn limit for this run. Must not exceed the
    /// definition's default.
    /// </param>
    /// <param name="attemptTimeout">
    /// An optional shorter attempt timeout for this run. Must not exceed the
    /// definition's default.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A supplied <paramref name="maxTurns"/> or <paramref name="attemptTimeout"/>
    /// is zero or negative.
    /// </exception>
    public AgentRunOptions(
        int? maxTurns = null,
        TimeSpan? attemptTimeout = null)
    {
        if (maxTurns is { } turns)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(turns, nameof(maxTurns));
        }

        if (attemptTimeout is { } timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                timeout,
                TimeSpan.Zero,
                nameof(attemptTimeout));
        }

        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
    }

    /// <summary>
    /// Gets the optional narrowed turn limit, or <see langword="null"/> to use
    /// the definition's default.
    /// </summary>
    public int? MaxTurns { get; init; }

    /// <summary>
    /// Gets the optional narrowed attempt timeout, or <see langword="null"/>
    /// to use the definition's default.
    /// </summary>
    public TimeSpan? AttemptTimeout { get; init; }
}
