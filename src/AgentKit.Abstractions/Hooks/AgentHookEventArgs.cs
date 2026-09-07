// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable, read-only identity and causality shared by every
/// boundary-specific hook event-argument type.
/// </summary>
/// <remarks>
/// <para>
/// Every concrete hook point defines its own <see cref="EventArgs"/>-derived
/// type deriving from this class, adding writable properties only for its
/// own documented transformations. This base class exposes stable agent,
/// session, causal-operation, timestamp, and hook-invocation identity; it
/// deliberately never exposes a mutable engine, unrestricted history, a
/// credential store, or a general service provider, so a hook cannot reach
/// outside the bounded surface its point was designed to expose.
/// </para>
/// <para>
/// This type carries no mutable state of its own and is safe to share
/// across threads without synchronization; a derived type's own writable
/// properties are mutated in place by hooks running sequentially under a
/// single dispatch and are never accessed concurrently by design.
/// </para>
/// </remarks>
public abstract class AgentHookEventArgs: EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="AgentHookEventArgs"/> class.</summary>
    /// <param name="agentId">The agent this hook invocation occurred for.</param>
    /// <param name="sessionId">The session this hook invocation relates to, when applicable.</param>
    /// <param name="correlation">The causal operation this hook invocation occurred within.</param>
    /// <param name="timestamp">The time this dispatch began, from the injected <see cref="TimeProvider"/>.</param>
    /// <param name="invocationId">The stable identity of this specific dispatch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="correlation"/> is null.</exception>
    protected AgentHookEventArgs(
        AgentId agentId,
        SessionId? sessionId,
        OperationCorrelation correlation,
        DateTimeOffset timestamp,
        HookInvocationId invocationId)
    {
        ArgumentNullException.ThrowIfNull(correlation);

        AgentId = agentId;
        SessionId = sessionId;
        Correlation = correlation;
        Timestamp = timestamp;
        InvocationId = invocationId;
    }

    /// <summary>Gets the agent this hook invocation occurred for.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session this hook invocation relates to, when applicable.</summary>
    public SessionId? SessionId { get; }

    /// <summary>Gets the causal operation this hook invocation occurred within.</summary>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the time this dispatch began, from the injected <see cref="TimeProvider"/>.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the stable identity of this specific dispatch.</summary>
    public HookInvocationId InvocationId { get; }

    /// <summary>
    /// Validates the current state of this instance's writable properties,
    /// throwing <see cref="HookValidationException"/> if a hook has left
    /// them in a state the owning hook point does not permit.
    /// </summary>
    /// <remarks>
    /// The dispatcher calls this after every hook invocation and before
    /// invoking the next one. The base implementation performs no
    /// validation; a derived type overrides it to check whatever invariants
    /// its own writable properties document.
    /// </remarks>
    /// <exception cref="HookValidationException">
    /// A writable property has been left in a state this hook point does
    /// not permit.
    /// </exception>
    public virtual void Validate()
    {
    }
}
