// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Process-local index of runs the engine has accepted and not yet finished driving.</summary>
/// <remarks>
/// Attach by <see cref="RunId"/> and durable cancel both need the exact session capability, in-run context, and
/// output publisher captured at acceptance. This registry is not durable truth; it exists only while the runtime
/// still owns the run scope and gate lease.
/// </remarks>
internal sealed class ActiveRunRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<RunId, ActiveRunRegistration> _runs = [];

    /// <summary>Registers one accepted run before the loop is driven.</summary>
    /// <param name="registration">The immutable acceptance evidence for the run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registration"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The same <see cref="RunId"/> is already registered.</exception>
    internal void Register(ActiveRunRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (_gate)
        {
            if (_runs.ContainsKey(registration.RunId))
            {
                throw new InvalidOperationException("An active run with the same RunId is already registered.");
            }

            _runs[registration.RunId] = registration;
        }
    }

    /// <summary>Removes one run after settlement, failure, or scope disposal.</summary>
    /// <param name="runId">The run identity to remove.</param>
    internal void Unregister(RunId runId)
    {
        lock (_gate)
        {
            _ = _runs.Remove(runId);
        }
    }

    /// <summary>Looks up one still-active run.</summary>
    /// <param name="runId">The run identity to resolve.</param>
    /// <param name="registration">The captured registration when the run is still active.</param>
    /// <returns><see langword="true"/> when the run is registered; otherwise <see langword="false"/>.</returns>
    internal bool TryGet(RunId runId, out ActiveRunRegistration registration)
    {
        lock (_gate)
        {
            return _runs.TryGetValue(runId, out registration!);
        }
    }
}

/// <summary>Evidence captured when one run is accepted and before the loop executes.</summary>
/// <param name="AgentId">The pinned agent that owns the run.</param>
/// <param name="SessionId">The session the run advances.</param>
/// <param name="ConversationId">The optional conversation correlation stored on the session.</param>
/// <param name="BranchId">The branch the run reads and commits on.</param>
/// <param name="RunId">The accepted run identity.</param>
/// <param name="Capability">The compiled session capability for this run scope.</param>
/// <param name="OperationContext">The lane-bound in-run operation context.</param>
/// <param name="PreviousCursor">The history cursor observed immediately before acceptance.</param>
/// <param name="SessionProfile">The immutable session profile snapshot for reads.</param>
/// <param name="Sessions">The session coordinator backing the capability.</param>
/// <param name="Publisher">The scoped output publisher when the composition registered one.</param>
internal sealed record ActiveRunRegistration(
    AgentId AgentId,
    SessionId SessionId,
    ConversationId? ConversationId,
    BranchId BranchId,
    RunId RunId,
    SessionExecutionCapability Capability,
    SessionOperationContext OperationContext,
    MessageCursor PreviousCursor,
    SessionProfileSnapshot SessionProfile,
    ISessionCoordinator Sessions,
    ISubscribableOutputPublisher? Publisher);
