// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The per-invocation facts a run needs that an agent definition cannot
/// supply, plus the bounded overrides a caller may apply.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// The split is deliberate: a definition is reusable and shared, while
/// session, branch, and execution identity belong to one invocation. Putting
/// them here is what allows a single immutable definition to serve many
/// concurrent runs for different users.
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
    private readonly ExecutionIdentity _identity;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRunOptions"/> record.
    /// </summary>
    /// <param name="sessionId">The session this run reads from and commits to.</param>
    /// <param name="branchId">The branch this run reads from and commits to.</param>
    /// <param name="identity">
    /// The already-authenticated identity on whose behalf the run is
    /// performed. AgentKit consumes this identity; it never authenticates it.
    /// </param>
    /// <param name="maxTurns">
    /// An optional lower turn limit for this run. Must not exceed the
    /// definition's default.
    /// </param>
    /// <param name="attemptTimeout">
    /// An optional shorter attempt timeout for this run. Must not exceed the
    /// definition's default.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sessionId"/> or <paramref name="branchId"/> is its
    /// default, empty identity, or a supplied <paramref name="maxTurns"/> or
    /// <paramref name="attemptTimeout"/> is zero or negative.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/> is <see langword="null"/>.
    /// </exception>
    public AgentRunOptions(
        SessionId sessionId,
        BranchId branchId,
        ExecutionIdentity identity,
        int? maxTurns = null,
        TimeSpan? attemptTimeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default, nameof(sessionId));
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default, nameof(branchId));
        ArgumentNullException.ThrowIfNull(identity);

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

        SessionId = sessionId;
        BranchId = branchId;
        _identity = identity;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
    }

    /// <summary>Gets the session this run reads from and commits to.</summary>
    public SessionId SessionId { get; init; }

    /// <summary>Gets the branch this run reads from and commits to.</summary>
    public BranchId BranchId { get; init; }

    /// <summary>Gets the authenticated identity the run is performed for.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ExecutionIdentity Identity
    {
        get => _identity;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Identity));
            _identity = value;
        }
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
