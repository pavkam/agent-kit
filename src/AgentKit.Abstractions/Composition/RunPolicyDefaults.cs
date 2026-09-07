// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The bounded run limits an agent definition applies when a caller does not
/// override them.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// These are defaults, not ceilings. A run may narrow them; whether a run may
/// widen them is a policy decision belonging to whichever component applies
/// the override, not to this value.
/// </para>
/// </remarks>
public sealed record RunPolicyDefaults
{
    /// <summary>
    /// A conservative default of eight turns and a two-minute attempt
    /// timeout.
    /// </summary>
    /// <value>
    /// Deliberately finite. An agent with no configured limits should stop on
    /// its own rather than loop until something else kills it.
    /// </value>
    public static RunPolicyDefaults Default { get; } = new(8, TimeSpan.FromMinutes(2));

    /// <summary>
    /// Initializes a new instance of the <see cref="RunPolicyDefaults"/>
    /// record.
    /// </summary>
    /// <param name="maxTurns">
    /// The maximum number of turns a run may take before it is halted.
    /// </param>
    /// <param name="attemptTimeout">
    /// The maximum duration allowed for one model attempt.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxTurns"/> is zero or negative, or
    /// <paramref name="attemptTimeout"/> is zero or negative. A run that may
    /// take no turns, or an attempt that has already timed out, is never a
    /// meaningful configuration.
    /// </exception>
    public RunPolicyDefaults(int maxTurns, TimeSpan attemptTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout, TimeSpan.Zero);

        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
    }

    /// <summary>Gets the maximum number of turns a run may take.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set a zero or negative value.
    /// </exception>
    public int MaxTurns
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(MaxTurns));
            field = value;
        }
    }

    /// <summary>Gets the maximum duration allowed for one model attempt.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An initializer attempts to set a zero or negative duration.
    /// </exception>
    public TimeSpan AttemptTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                value,
                TimeSpan.Zero,
                nameof(AttemptTimeout));
            field = value;
        }
    }
}
