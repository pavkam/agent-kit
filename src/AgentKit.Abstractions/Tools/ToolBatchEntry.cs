// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One already resolved, validated, and authorized call ready for an <see cref="IToolScheduler"/> to invoke.</summary>
/// <remarks>
/// The scheduler receives already prepared invoker handles; it never resolves an <see cref="IServiceProvider"/> or
/// reimplements resolution. <see cref="SourceOrdinal"/> establishes deterministic publication order independently
/// of completion order. This entry owns <see cref="InvokerLease"/> only for the duration of scheduling and
/// invocation; its owner must release it once the entry's terminal result is produced. This type is an immutable
/// value object with structural equality over its fields and is safe to share across threads without
/// synchronization, though <see cref="InvokerLease"/> itself is not concurrency-safe to invoke twice.
/// </remarks>
public sealed record ToolBatchEntry
{
    /// <summary>Initializes one immutable prepared batch entry.</summary>
    /// <param name="invocation">The nonnull already validated and authorized invocation context.</param>
    /// <param name="invokerLease">The nonnull owned invoker lease bound to <paramref name="invocation"/>'s exact tool.</param>
    /// <param name="executionHints">The nonnull untrusted scheduling hints declared for the resolved tool.</param>
    /// <param name="sourceOrdinal">The nonnegative provider-emitted source ordinal.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="invocation"/>, <paramref name="invokerLease"/>, or <paramref name="executionHints"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceOrdinal"/> is negative.</exception>
    public ToolBatchEntry(
        ToolInvocationContext invocation,
        IToolInvokerLease invokerLease,
        ToolExecutionHints executionHints,
        int sourceOrdinal)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(invokerLease);
        ArgumentNullException.ThrowIfNull(executionHints);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOrdinal);

        Invocation = invocation;
        InvokerLease = invokerLease;
        ExecutionHints = executionHints;
        SourceOrdinal = sourceOrdinal;
    }

    /// <summary>Gets the already validated and authorized invocation context.</summary>
    /// <value>The exact context the scheduler passes unchanged to <see cref="IToolInvokerLease.Invoker"/>.</value>
    public ToolInvocationContext Invocation { get; }

    /// <summary>Gets the owned invoker lease bound to this entry's exact tool.</summary>
    /// <value>A lease matching <see cref="Invocation"/>'s resolved descriptor and source version.</value>
    public IToolInvokerLease InvokerLease { get; }

    /// <summary>Gets the untrusted scheduling hints declared for the resolved tool.</summary>
    /// <value>Advisory evidence; host policy may tighten it and must treat absent evidence conservatively.</value>
    public ToolExecutionHints ExecutionHints { get; }

    /// <summary>Gets the provider-emitted source ordinal.</summary>
    /// <value>A nonnegative ordinal establishing deterministic publication order among sibling entries.</value>
    public int SourceOrdinal { get; }
}
