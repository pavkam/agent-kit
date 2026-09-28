// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Everything one invocation of an <see cref="IDurableOperationHandler"/> is
/// given: the accepted declaration, the ownership it runs under, the
/// coordinator-owned writer for mid-operation evidence, and the live hook
/// context when an active run exists.
/// </summary>
/// <remarks>
/// <para>
/// The context is assembled by the coordinator for exactly one attempt and is
/// never serialized. It holds a live lease and a live writer, so it is not
/// durable state and must not be stored on a descriptor, checkpoint, captured
/// execution context, or recovery evidence.
/// </para>
/// <para>
/// A handler receives this rather than the journal, the runtime lease, or the
/// security authority. Acceptance, dispatch, and terminal commit stay with the
/// coordinator; the handler performs the effect and, where the effect has
/// meaningful intermediate boundaries, records them through
/// <see cref="Checkpoints"/>.
/// </para>
/// <para>
/// The instance is immutable and safe to read concurrently, but the lease and
/// writer it exposes are valid only for the duration of the invocation.
/// </para>
/// </remarks>
public sealed record DurableInvocationContext
{
    /// <summary>Initializes one coherent handler invocation context.</summary>
    /// <param name="operation">The non-null immutable declaration whose acceptance the coordinator already committed.</param>
    /// <param name="lease">The non-null active execution lease presenting this attempt's fencing generation.</param>
    /// <param name="checkpoints">
    /// The non-null coordinator-owned writer for mid-operation checkpoints and waiting records. Its
    /// <see cref="IDurableCheckpointWriter.Binding"/> must equal <paramref name="operation"/>'s binding.
    /// </param>
    /// <param name="hooks">
    /// The live hook dispatch context, or <see langword="null"/> when no active run exists. Recovery without a run
    /// passes <see langword="null"/> rather than fabricating a run-scoped tracker.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operation"/>, <paramref name="lease"/>, or <paramref name="checkpoints"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="checkpoints"/> is bound to a different operation than <paramref name="operation"/>, or
    /// <paramref name="lease"/> does not present the writer's fencing generation. Either mismatch would let a
    /// handler write evidence about work it is not performing.
    /// </exception>
    public DurableInvocationContext(
        RecoverableOperationDescriptor operation,
        IExecutionLease lease,
        IDurableCheckpointWriter checkpoints,
        HookDispatchContext? hooks = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(checkpoints);
        if (checkpoints.Binding != operation.Binding)
        {
            throw new ArgumentException(
                "The supplied checkpoint writer is bound to a different durable operation than the declaration.",
                nameof(checkpoints));
        }

        if (lease.FencingToken != checkpoints.FencingToken)
        {
            throw new ArgumentException(
                "The supplied execution lease does not present the ownership generation the checkpoint writer writes under.",
                nameof(lease));
        }

        Operation = operation;
        Lease = lease;
        Checkpoints = checkpoints;
        Hooks = hooks;
    }

    /// <summary>Gets the accepted immutable declaration being invoked.</summary>
    /// <value>The exact descriptor the coordinator committed a start record for.</value>
    public RecoverableOperationDescriptor Operation { get; }

    /// <summary>Gets the active execution lease for this attempt.</summary>
    /// <value>
    /// The lease whose <see cref="IExecutionLease.FencingToken"/> every durable write for this attempt carries.
    /// Renewal is the coordinator's responsibility; a handler reads the lease but never extends ownership itself.
    /// </value>
    public IExecutionLease Lease { get; }

    /// <summary>Gets the coordinator-owned writer for mid-operation durable evidence.</summary>
    /// <value>
    /// A writer bound to <see cref="Operation"/> and valid only for the duration of this invocation. It is the only
    /// way a handler commits a checkpoint or waiting record.
    /// </value>
    public IDurableCheckpointWriter Checkpoints { get; }

    /// <summary>Gets the live hook dispatch context, when an active run exists.</summary>
    /// <value>
    /// The run's hook context for a live invocation, or <see langword="null"/> during recovery with no active run.
    /// It is never serialized into any durable record.
    /// </value>
    public HookDispatchContext? Hooks { get; }

    /// <summary>Gets the exact durable binding derived from <see cref="Operation"/>.</summary>
    /// <value>The inseparable address and captured context this attempt writes every record under.</value>
    public DurableOperationBinding Binding => Operation.Binding;

    /// <summary>Gets the operation coordinates derived from <see cref="Operation"/>.</summary>
    /// <value>The typed address that locates this operation's durable records.</value>
    public DurableOperationAddress Address => Operation.Address;
}
