// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Executes one recoverable operation kind under an acquired execution lease.</summary>
/// <remarks>
/// <para>
/// A handler owns the effect and nothing else. Acceptance, external handoff, and the terminal record belong to the
/// coordinator, which invokes exactly one handler per recoverable operation name.
/// </para>
/// <para>
/// Mid-operation evidence is written through <see cref="DurableInvocationContext.Checkpoints"/>. That writer, not the
/// journal or the lease, is what lets a handler record a semantic boundary it reached or a condition it is waiting
/// on without ever holding the journal, the captured grant, or the checkpoint-identity generator.
/// </para>
/// </remarks>
public interface IDurableOperationHandler
{
    /// <summary>Gets the operation name this handler owns.</summary>
    /// <value>The single recoverable operation name no other registered handler may claim.</value>
    public DurableOperationName OperationName { get; }

    /// <summary>Performs the effect and returns its terminal durable result.</summary>
    /// <param name="context">
    /// The non-null invocation context carrying the accepted declaration, the active lease, the coordinator-owned
    /// checkpoint writer, and the live hook context when an active run exists. It is valid only for the duration of
    /// this call.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels local awaiting only. It never asserts that an already-started external effect stopped, so a handler
    /// that observes cancellation after starting an effect reports truthful side-effect certainty rather than
    /// claiming the effect did not occur.
    /// </param>
    /// <returns>
    /// The non-null terminal durable result for this attempt. The coordinator commits it; returning
    /// <see langword="null"/> is a contract violation the coordinator refuses.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public ValueTask<DurableOperationResult> InvokeAsync(
        DurableInvocationContext context,
        CancellationToken cancellationToken = default);
}
