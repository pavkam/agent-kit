// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable, read-only point, dispatch, causality, and timing identity shared by every
/// boundary-specific hook event-argument type, regardless of whether that
/// boundary's stage has resolved an agent yet.
/// </summary>
/// <remarks>
/// <para>
/// Every concrete hook point defines its own <see cref="EventArgs"/>-derived
/// type deriving from this class (typically through the intermediate
/// <see cref="AgentScopedHookEventArgs"/> once its stage has an agent),
/// adding writable properties only for its own documented transformations.
/// This base class deliberately omits an agent or session identity, because
/// engine construction can precede an <see cref="AgentId"/>, agent
/// registration can precede a <see cref="SessionId"/>, and
/// admission can precede a <see cref="RunId"/>; a base that
/// required them would force an earlier-stage hook point to fabricate a
/// value it does not truthfully have. It also never exposes a mutable
/// engine, unrestricted history, a credential store, or a general service
/// provider, so a hook cannot reach outside the bounded surface its point
/// was designed to expose.
/// </para>
/// <para>
/// One instance is shared across every hook invoked for one
/// <see cref="DispatchId"/>, so it cannot truthfully expose a single
/// registration's individual execution identity. The dispatcher builds a
/// separate immutable <see cref="HookInvocationContext"/> for each hook it
/// invokes and supplies that alongside this shared instance.
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
    /// <param name="dispatch">The point identity, dispatch identity, causality, and timing facts for this dispatch.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> is null.</exception>
    protected AgentHookEventArgs(HookDispatchMetadata dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        Point = dispatch.Point;
        DispatchId = dispatch.DispatchId;
        Correlation = dispatch.Correlation;
        Timestamp = dispatch.Timestamp;
        Deadline = dispatch.Deadline;
    }

    /// <summary>Gets the hook point this dispatch targets.</summary>
    public HookPointId Point { get; }

    /// <summary>Gets the stable identity of this dispatch, shared by every hook invoked for it.</summary>
    public HookDispatchId DispatchId { get; }

    /// <summary>Gets the causal operation this hook invocation occurred within.</summary>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the time this dispatch began, from the injected <see cref="TimeProvider"/>.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the latest time by which this dispatch's hooks are expected to have quiesced.</summary>
    /// <value>Clamped by the kernel to at most the host's configured default hook timeout past <see cref="Timestamp"/>.</value>
    public DateTimeOffset Deadline { get; }

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

    /// <summary>
    /// Captures every writable property this hook point permits so the dispatcher can roll a failed,
    /// isolated hook's partial mutation back before the next hook runs.
    /// </summary>
    /// <remarks>
    /// The base implementation captures nothing and returns <see langword="null"/>, which is correct only
    /// for an argument type with no writable state. A derived type that exposes writable properties
    /// (including a short-circuit marker) MUST override this member together with
    /// <see cref="RestoreMutableState"/>; otherwise an isolated failure may leak a partial mutation to
    /// later hooks and to the owning operation.
    /// </remarks>
    /// <returns>An opaque immutable snapshot understood only by <see cref="RestoreMutableState"/>, or <see langword="null"/> when there is no writable state.</returns>
    public virtual object? CaptureMutableState() => null;

    /// <summary>Restores the writable properties captured by <see cref="CaptureMutableState"/>.</summary>
    /// <param name="snapshot">The value previously returned by <see cref="CaptureMutableState"/> on this instance.</param>
    /// <remarks>The base implementation does nothing; see <see cref="CaptureMutableState"/> for when an override is required.</remarks>
    public virtual void RestoreMutableState(object? snapshot)
    {
    }
}
