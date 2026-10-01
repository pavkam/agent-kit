// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provisions and executes one delegated child attempt for a host worker.</summary>
/// <remarks>
/// <para>
/// A runner is a host component, not a dispatcher: it is invoked only by a worker that already claimed the attempt, so it
/// never decides whether a child should run and never mutates goal state. It executes the child under the delegation's
/// captured identity, deadline, and budget ceilings, and reports a bounded result. Provisioning is separate from running so
/// the worker can record the real session in the attempt before any effect happens.
/// </para>
/// <para>Implementations are thread-safe: a worker may run several children concurrently.</para>
/// </remarks>
public interface IDelegationChildRunner
{
    /// <summary>Creates, or finds, the session one attempt executes in.</summary>
    /// <param name="delegation">The canonical delegation.</param>
    /// <param name="childGoalId">The child goal.</param>
    /// <param name="attemptNumber">The one-based attempt number; the session is idempotent per goal and attempt number.</param>
    /// <param name="cancellationToken">Cancels provisioning.</param>
    /// <returns>The session, or <see langword="null"/> when the target cannot run the child on this host.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="delegation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="childGoalId"/> is default or <paramref name="attemptNumber"/> is not positive.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<SessionId?> ProvisionSessionAsync(DelegationRequest delegation, GoalId childGoalId, int attemptNumber, CancellationToken cancellationToken = default);

    /// <summary>Executes one claimed attempt to a terminal result.</summary>
    /// <param name="request">The claimed attempt.</param>
    /// <param name="cancellationToken">Cancels the wait and asks the run to stop; the delegation deadline is enforced by the caller through this token.</param>
    /// <returns>The terminal result. A refusal to admit the run is a <see cref="DelegationStatus.Failed"/> result that performed no side effect, never an exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<DelegationChildRunResult> RunAsync(DelegationChildRunRequest request, CancellationToken cancellationToken = default);
}
