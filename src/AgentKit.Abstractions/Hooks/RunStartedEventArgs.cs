// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Event arguments for <see cref="AgentHookPoints.RunStarted"/>: the identities and bounds of a run that is about
/// to drive its first turn. Every member is read-only.
/// </summary>
/// <remarks>
/// Raised after admission, session load, and model resolution succeeded, so the run, branch, and selected model are
/// established facts. Hooks at this point observe; they cannot change the run.
/// </remarks>
public sealed class RunStartedEventArgs: AgentHookEventArgs, IAgentScopedHookStage
{
    /// <summary>Initializes the arguments.</summary>
    /// <param name="dispatch">The point identity, dispatch identity, causality, and timing facts for this dispatch.</param>
    /// <param name="agentId">The agent being run.</param>
    /// <param name="sessionId">The session the run appends to.</param>
    /// <param name="branchId">The branch the run appends to.</param>
    /// <param name="model">The model selected for every turn of the run.</param>
    /// <param name="maxTurns">The run's turn limit.</param>
    /// <param name="attemptTimeout">The run's per-attempt timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> or <paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="dispatch"/>'s correlation is not an in-run correlation.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="branchId"/> is default, <paramref name="maxTurns"/> is not positive, or <paramref name="attemptTimeout"/> is not positive.</exception>
    public RunStartedEventArgs(
        HookDispatchMetadata dispatch,
        AgentId agentId,
        SessionId sessionId,
        BranchId branchId,
        ModelDescriptor model,
        int maxTurns,
        TimeSpan attemptTimeout)
        : base(dispatch)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentException.ThrowIfNotEqual(Correlation is InRunOperationCorrelation, true, nameof(dispatch));
        AgentId = agentId;
        SessionId = sessionId;
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout, TimeSpan.Zero);
        BranchId = branchId;
        Model = model;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
    }

    /// <inheritdoc/>
    public AgentId AgentId { get; }

    /// <inheritdoc/>
    public SessionId? SessionId { get; }

    /// <summary>Gets the run identity.</summary>
    /// <value>Read through the base <see cref="AgentHookEventArgs.Correlation"/>, which the constructor requires to be an in-run correlation; never a second stored copy.</value>
    public RunId RunId => ((InRunOperationCorrelation) Correlation).RunId;

    /// <summary>Gets the branch the run appends to.</summary>
    public BranchId BranchId { get; }

    /// <summary>Gets the model selected for every turn of the run.</summary>
    public ModelDescriptor Model { get; }

    /// <summary>Gets the run's turn limit.</summary>
    public int MaxTurns { get; }

    /// <summary>Gets the run's per-attempt timeout.</summary>
    public TimeSpan AttemptTimeout { get; }
}
