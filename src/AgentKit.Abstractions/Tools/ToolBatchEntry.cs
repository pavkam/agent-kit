// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One already resolved, validated, planned, authorized, and recorded call ready for an <see cref="IToolScheduler"/> to invoke.</summary>
/// <remarks>
/// <para>
/// The scheduler receives already prepared invoker handles; it never resolves an <see cref="IServiceProvider"/> or
/// reimplements resolution. <see cref="SourceOrdinal"/> establishes deterministic publication order independently
/// of completion order. The entry retains the <see cref="Prepared"/> call and the durable <see cref="Accepted"/> record
/// so the scheduler builds the terminal record from the exact admission, acceptance, and policy evidence rather than
/// from the invoker context. This entry owns <see cref="InvokerLease"/> only for the duration of scheduling and
/// invocation; its owner must release it once the entry's terminal result is produced. This type is an immutable
/// value object with structural equality over its fields and is safe to share across threads without
/// synchronization, though <see cref="InvokerLease"/> itself is not concurrency-safe to invoke twice.
/// </para>
/// </remarks>
public sealed record ToolBatchEntry
{
    /// <summary>Initializes one immutable prepared batch entry.</summary>
    /// <param name="invocation">The nonnull already validated and authorized invocation context.</param>
    /// <param name="invokerLease">The nonnull owned invoker lease bound to <paramref name="invocation"/>'s exact tool.</param>
    /// <param name="prepared">The nonnull validated call and plan the invocation was built from.</param>
    /// <param name="accepted">The nonnull accepted-call record that was committed before this entry was scheduled.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The call identity, resolved tool, or tool version of <paramref name="invocation"/>, <paramref name="prepared"/>,
    /// and <paramref name="accepted"/> do not agree, or <paramref name="invokerLease"/> was not acquired for the same
    /// descriptor.
    /// </exception>
    public ToolBatchEntry(
        ToolInvocationContext invocation,
        IToolInvokerLease invokerLease,
        PreparedToolCall prepared,
        AcceptedToolCall accepted)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(invokerLease);
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentException.ThrowIfNotEqual(prepared.Call.CallId, invocation.CallId, nameof(prepared));
        ArgumentException.ThrowIfNotEqual(accepted.CallId, invocation.CallId, nameof(accepted));
        ArgumentException.ThrowIfNotEqual(accepted.ToolId, invocation.Tool.Id, nameof(accepted));
        ArgumentException.ThrowIfNotEqual(accepted.ToolVersion, invocation.ToolVersion, nameof(accepted));
        ArgumentException.ThrowIfNotEqual(prepared.Call.ToolVersion, invocation.ToolVersion, nameof(prepared));
        ArgumentException.ThrowIfNotEqual(accepted.Normalization, prepared.ExecutionPlan.Normalization, nameof(accepted));

        Invocation = invocation;
        InvokerLease = invokerLease;
        Prepared = prepared;
        Accepted = accepted;
    }

    /// <summary>Gets the already validated and authorized invocation context.</summary>
    /// <value>The exact context the scheduler passes to <see cref="IToolInvokerLease.Invoker"/>, re-issued with a higher attempt for a retry.</value>
    public ToolInvocationContext Invocation { get; }

    /// <summary>Gets the owned invoker lease bound to this entry's exact tool.</summary>
    /// <value>A lease matching <see cref="Invocation"/>'s resolved descriptor and source version.</value>
    public IToolInvokerLease InvokerLease { get; }

    /// <summary>Gets the validated call and the plan its execution policy produced.</summary>
    /// <value>The retained evidence the normalizer and the terminal record are built from.</value>
    public PreparedToolCall Prepared { get; }

    /// <summary>Gets the accepted-call record committed before scheduling.</summary>
    /// <value>The durable admission, acceptance, and policy evidence every terminal record of this call must carry.</value>
    public AcceptedToolCall Accepted { get; }

    /// <summary>Gets the effective scheduling hints planned for the resolved tool.</summary>
    /// <value>The policy-planned hints; host policy may have tightened the descriptor's own hints and absent evidence is treated conservatively.</value>
    public ToolExecutionHints ExecutionHints => Prepared.ExecutionPlan.Scheduling;

    /// <summary>Gets the provider-emitted source ordinal.</summary>
    /// <value>A nonnegative ordinal establishing deterministic publication order among sibling entries.</value>
    public int SourceOrdinal => Prepared.Call.SourceOrdinal;
}
