// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Invokes the live continuation published for one in-process boundary's operation identity.</summary>
/// <remarks>
/// <para>
/// The handler performs no effect of its own. It exists so the durability coordinator can own acceptance, dispatch,
/// and terminal commit around a boundary whose effect only the running component can perform. When no continuation
/// is published — a different process, or an attempt this process already settled — it refuses rather than inventing
/// a terminal record, because a fabricated result would be journaled as authoritative truth about an effect nobody
/// ran.
/// </para>
/// <para>
/// This base class is offered because the mechanics are identical for every such boundary; it is not the only
/// extension path. A handler that owns a real out-of-process effect implements <see cref="IDurableOperationHandler"/>
/// directly instead of borrowing a registry it has no use for.
/// </para>
/// <para>
/// Instances are engine-wide singletons and safe for concurrent use.
/// </para>
/// </remarks>
public abstract class DurableBoundaryHandler: IDurableOperationHandler
{
    private readonly DurableBoundaryRegistry _registry;

    /// <summary>Initializes a handler bound to one recoverable operation name.</summary>
    /// <param name="operationName">The nonblank operation name this handler owns exclusively.</param>
    /// <param name="registry">The non-null engine-wide registry of live boundary continuations, borrowed and never disposed here.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="operationName"/> carries no name text.</exception>
    /// <remarks>
    /// Each derived handler is a distinct type so additive dependency-injection registration stays idempotent per
    /// boundary and the coordinator's one-handler-per-name rule is enforced at composition.
    /// </remarks>
    protected DurableBoundaryHandler(DurableOperationName operationName, DurableBoundaryRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName.Value, nameof(operationName));
        ArgumentNullException.ThrowIfNull(registry);
        OperationName = operationName;
        _registry = registry;
    }

    /// <inheritdoc/>
    /// <value>The single boundary name this handler answers for.</value>
    public DurableOperationName OperationName { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The declaration names a different operation, or no live continuation is published for its identity. The
    /// latter is the normal outcome in a recovering process and leaves the durable record untouched.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public virtual ValueTask<DurableOperationResult> InvokeAsync(
        DurableInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return context.Operation.Name != OperationName
            ? throw new InvalidOperationException(
                "The durable operation handler was asked to invoke an operation name it does not own.")
            : _registry.Resolve(context.Address.OperationId) is { } invocation
                ? invocation(context, cancellationToken)
                : throw new InvalidOperationException(
                    "No live boundary continuation is registered for this durable operation, so it cannot be invoked here.");
    }
}
