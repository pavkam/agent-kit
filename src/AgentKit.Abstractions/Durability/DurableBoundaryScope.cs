// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Wraps in-process boundaries in recoverable operations under one selected durability profile.</summary>
/// <remarks>
/// <para>
/// A scope exists only for work whose agent definition selects a durability profile. It resolves that profile once,
/// so every operation it journals records the same captured component selection, and recovery rebinds exactly those
/// components rather than whatever the agent is configured with later.
/// </para>
/// <para>
/// A boundary is journaled only when the profile enables its operation name. An unlisted boundary runs exactly as it
/// does in an undurable composition, which is what makes durability additive: selecting a profile never changes what
/// a component computes, only what evidence survives the process.
/// </para>
/// <para>
/// Each boundary's durable address is derived from that boundary's own authorization capture rather than supplied
/// beside it, so an address can never name work the capture does not authorize. Before-run and sessionless captures
/// are refused, because the present address shape cannot represent them and inventing a run identity would make
/// recovery attribute work to a run that never contained it.
/// </para>
/// <para>
/// The scope is created per run or per maintenance attempt but publishes continuations into an engine-wide registry,
/// so concurrent work may safely hold its own scopes over the same coordinator. This is reusable mechanics, not a
/// replaceable policy: the decisions it composes — <see cref="IDurableExecutionCoordinator"/>,
/// <see cref="IDurabilityProfileCatalog"/>, and the profile's own component keys — remain separately registered
/// contracts.
/// </para>
/// </remarks>
public sealed class DurableBoundaryScope
{
    private readonly IDurableExecutionCoordinator _coordinator;
    private readonly DurableBoundaryRegistry _registry;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _operationTimeout;

    private DurableBoundaryScope(
        IDurableExecutionCoordinator coordinator,
        DurableBoundaryRegistry registry,
        DurabilityProfileSnapshot profile,
        TimeProvider timeProvider,
        TimeSpan operationTimeout)
    {
        Debug.Assert(coordinator is not null, "A coordinator is required for a durable boundary scope.");
        Debug.Assert(registry is not null, "A live continuation registry is required for a durable boundary scope.");
        Debug.Assert(profile is not null, "A resolved durability profile is required for a durable boundary scope.");
        Debug.Assert(timeProvider is not null, "An injected clock is required for a durable boundary scope.");
        Debug.Assert(operationTimeout > TimeSpan.Zero, "A positive per-operation deadline window is required.");
        _coordinator = coordinator;
        _registry = registry;
        Profile = profile;
        _timeProvider = timeProvider;
        _operationTimeout = operationTimeout;
    }

    /// <summary>Gets the profile every operation this scope journals was captured under.</summary>
    /// <value>The resolved immutable snapshot, fixed for the scope's lifetime so recovery rebinds the same components.</value>
    public DurabilityProfileSnapshot Profile { get; }

    /// <summary>Resolves a durable boundary scope, or reports that this work journals nothing.</summary>
    /// <param name="profileKey">The profile the agent definition or component options selected, or <see langword="null"/> when none was selected.</param>
    /// <param name="coordinator">The composed coordinator, or <see langword="null"/> when durability is not composed.</param>
    /// <param name="profiles">The composed profile catalog, or <see langword="null"/> when durability is not composed.</param>
    /// <param name="registry">The non-null engine-wide live continuation registry.</param>
    /// <param name="timeProvider">The non-null injected clock used for operation deadlines and record instants.</param>
    /// <param name="operationTimeout">The positive window added to the clock for each operation's declared deadline.</param>
    /// <param name="scope">The resolved scope when a profile was selected and honored; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// A safe failure reason when a selected profile cannot be honored; otherwise <see langword="null"/>. A
    /// <see langword="null"/> return with a <see langword="null"/> <paramref name="scope"/> means no profile was
    /// selected and nothing is journaled.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationTimeout"/> is not positive.</exception>
    /// <remarks>
    /// Work that selects a profile the composition cannot resolve fails rather than silently running undurably: the
    /// selection is a promise that evidence will exist, and quietly dropping it would make a later recovery report
    /// started work as never started.
    /// </remarks>
    public static string? TryCreate(
        DurabilityProfileKey? profileKey,
        IDurableExecutionCoordinator? coordinator,
        IDurabilityProfileCatalog? profiles,
        DurableBoundaryRegistry registry,
        TimeProvider timeProvider,
        TimeSpan operationTimeout,
        out DurableBoundaryScope? scope)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(operationTimeout, TimeSpan.Zero, nameof(operationTimeout));

        scope = null;
        if (profileKey is not { } key)
        {
            return null;
        }

        if (coordinator is null || profiles is null)
        {
            return "A durability profile is selected but no durability runtime is composed.";
        }

        if (!profiles.TryGet(key, out var profile))
        {
            return "The selected durability profile is not registered.";
        }

        scope = new DurableBoundaryScope(coordinator, registry, profile, timeProvider, operationTimeout);
        return null;
    }

    /// <summary>Reports whether the resolved profile enables one boundary.</summary>
    /// <param name="operationName">The boundary's recoverable operation name.</param>
    /// <returns><see langword="true"/> when the profile listed the name; otherwise <see langword="false"/>.</returns>
    public bool Enables(DurableOperationName operationName) => Profile.EnabledOperations.Contains(operationName);

    /// <summary>
    /// Reports whether one boundary is journaled at all: its name is enabled
    /// and its capture can be durably addressed.
    /// </summary>
    /// <param name="operationName">The boundary's recoverable operation name.</param>
    /// <param name="authorization">The non-null capture the boundary would journal under.</param>
    /// <returns><see langword="true"/> when <see cref="ExecuteAsync"/> would journal this boundary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Callers use this to keep their undurable path unchanged instead of discovering an unaddressable capture from
    /// an exception in the middle of the boundary.
    /// </remarks>
    public bool Journals(DurableOperationName operationName, SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        return Enables(operationName) && DurableOperationAddress.TryCreateFrom(authorization, out _);
    }

    /// <summary>Runs one boundary as a recoverable operation and returns the boundary's own result.</summary>
    /// <typeparam name="TResult">The result the wrapped boundary produces, which is returned unchanged.</typeparam>
    /// <param name="operationName">The enabled recoverable operation name.</param>
    /// <param name="operationVersion">The published version of the encoded state shape.</param>
    /// <param name="authorization">
    /// The non-null capture every durable write for this boundary runs under. Its scope also determines the
    /// operation's durable address, so the capture must be scoped to this boundary's own operation identity.
    /// </param>
    /// <param name="input">The non-null versioned manifest describing the boundary's work.</param>
    /// <param name="effect">The protected effect the boundary performs.</param>
    /// <param name="hooks">The active hook dispatch context, or <see langword="null"/> when hooks are not composed.</param>
    /// <param name="body">
    /// The non-null boundary the caller would run anyway. It receives the coordinator-assembled
    /// <see cref="DurableInvocationContext"/> so it can commit mid-operation checkpoints and waiting records.
    /// </param>
    /// <param name="cancellationToken">Cancels local awaiting.</param>
    /// <returns>Exactly the value <paramref name="body"/> produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/>, <paramref name="input"/>, or <paramref name="body"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationName"/>, <paramref name="operationVersion"/>, or <paramref name="effect"/> is default or undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="authorization"/> is sessionless or before-run and cannot be durably addressed.</exception>
    /// <exception cref="InvalidOperationException">
    /// The coordinator settled the operation without invoking this process's continuation, so no boundary result
    /// exists. This is how a fabricated result is prevented rather than returned.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// The durable record describes the effect attempt, not the boundary's semantic outcome: a model attempt that
    /// returns a normalized failure still performed provider egress, and recording that as a non-effect would let
    /// recovery retry work that already cost something. Semantic outcomes stay in the caller's own result types.
    /// </remarks>
    public async ValueTask<TResult> ExecuteAsync<TResult>(
        DurableOperationName operationName,
        DurableOperationVersion operationVersion,
        SecurityAuthorizationContext authorization,
        OperationPayload input,
        SecurityEffect effect,
        HookDispatchContext? hooks,
        Func<DurableInvocationContext, CancellationToken, ValueTask<TResult>> body,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationName, default, nameof(operationName));
        ArgumentOutOfRangeException.ThrowIfEqual(operationVersion, default, nameof(operationVersion));
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentNullException.ThrowIfNull(body);
        Debug.Assert(Enables(operationName), "Only an enabled operation name reaches durable execution.");

        if (!DurableOperationAddress.TryCreateFrom(authorization, out var address))
        {
            throw new ArgumentException(
                "The supplied authorization capture is sessionless or before-run and cannot be durably addressed.",
                nameof(authorization));
        }

        var operationId = address.OperationId;
        var descriptor = new RecoverableOperationDescriptor(
            address,
            new DurableExecutionContext(
                Profile.Key,
                Profile.Version,
                Profile.BackendKey,
                Profile.JournalKey,
                Profile.LeaseManagerKey,
                Profile.RecoveryPolicyKey,
                authorization),
            operationName,
            operationVersion,
            new IdempotencyKey(operationId.ToString()),
            input,
            DurableRetryOwner.Caller,
            DurableTimeoutOwner.Caller,
            CancellationSemantics.LocalWaitOnly,
            effect,
            IdempotencyClassification.NonIdempotent,
            _timeProvider.GetUtcNow() + _operationTimeout);

        var capture = new BoundaryCapture<TResult>();
        using (_registry.Register(operationId, (context, token) => InvokeAsync(capture, context, body, token)))
        {
            _ = await _coordinator.ExecuteAsync(descriptor, hooks, cancellationToken).ConfigureAwait(false);
        }

        return capture.Completed
            ? capture.Result!
            : throw new InvalidOperationException(
                "The durable coordinator settled a boundary without running it in this process.");
    }

    private async ValueTask<DurableOperationResult> InvokeAsync<TResult>(
        BoundaryCapture<TResult> capture,
        DurableInvocationContext context,
        Func<DurableInvocationContext, CancellationToken, ValueTask<TResult>> body,
        CancellationToken cancellationToken)
    {
        Debug.Assert(capture is not null, "A boundary capture is required.");
        Debug.Assert(context is not null, "The coordinator supplies the invocation context.");
        capture.Result = await body(context, cancellationToken).ConfigureAwait(false);
        capture.Completed = true;
        return new DurableOperationResult(
            context.Binding,
            DurableOperationState.Completed,
            SideEffectCertainty.DefinitelyPerformed,
            DurableBoundaryPayload.Encode(new DurableBoundaryOutcome(context.Operation.Name.Value)),
            context.Lease.FencingToken,
            _timeProvider.GetUtcNow());
    }

    /// <summary>Carries one boundary's own result out of the coordinator-owned invocation.</summary>
    /// <typeparam name="TResult">The result the boundary produced.</typeparam>
    private sealed class BoundaryCapture<TResult>
    {
        /// <summary>Gets or sets the boundary's result once it ran.</summary>
        public TResult? Result { get; set; }

        /// <summary>Gets or sets whether the boundary actually ran in this process.</summary>
        public bool Completed { get; set; }
    }
}
