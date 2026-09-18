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
public sealed class RunStartedEventArgs: AgentHookEventArgs
{
    /// <summary>Initializes the arguments.</summary>
    /// <param name="agentId">The agent being run.</param>
    /// <param name="sessionId">The session the run appends to.</param>
    /// <param name="correlation">The run's in-run correlation.</param>
    /// <param name="timestamp">When the dispatch began.</param>
    /// <param name="invocationId">The dispatch's invocation identity.</param>
    /// <param name="branchId">The branch the run appends to.</param>
    /// <param name="model">The model selected for every turn of the run.</param>
    /// <param name="maxTurns">The run's turn limit.</param>
    /// <param name="attemptTimeout">The run's per-attempt timeout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="correlation"/> or <paramref name="model"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="branchId"/> is default, <paramref name="maxTurns"/> is not positive, or <paramref name="attemptTimeout"/> is not positive.</exception>
    public RunStartedEventArgs(
        AgentId agentId,
        SessionId sessionId,
        InRunOperationCorrelation correlation,
        DateTimeOffset timestamp,
        HookInvocationId invocationId,
        BranchId branchId,
        ModelDescriptor model,
        int maxTurns,
        TimeSpan attemptTimeout)
        : base(agentId, sessionId, correlation, timestamp, invocationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(branchId, default);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(attemptTimeout, TimeSpan.Zero);
        RunId = correlation.RunId;
        BranchId = branchId;
        Model = model;
        MaxTurns = maxTurns;
        AttemptTimeout = attemptTimeout;
    }

    /// <summary>Gets the run identity.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the branch the run appends to.</summary>
    public BranchId BranchId { get; }

    /// <summary>Gets the model selected for every turn of the run.</summary>
    public ModelDescriptor Model { get; }

    /// <summary>Gets the run's turn limit.</summary>
    public int MaxTurns { get; }

    /// <summary>Gets the run's per-attempt timeout.</summary>
    public TimeSpan AttemptTimeout { get; }
}
