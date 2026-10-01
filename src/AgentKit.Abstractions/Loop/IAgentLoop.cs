// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Coordinates one run: preparing each turn's context, calling the model,
/// executing accepted tool calls, committing every result, and deciding
/// whether another turn is required.
/// </summary>
/// <remarks>
/// <para>
/// The run is request-based: the caller supplies one pinned
/// <see cref="AgentLoopRunRequest"/> and the per-run <see cref="AgentRunServices"/>
/// the facade compiled from the definition's keyed selections. The caller has
/// already durably admitted the run's initial input; an implementation promotes
/// further queued input through the compiled <see cref="IInputCoordinator"/>
/// at safe boundaries, reserves usage through the budget authority when the
/// definition's profile declares limits, publishes progress through the
/// compiled output publisher, and validates terminal output through the
/// compiled output processor.
/// </para>
/// <para>
/// An implementation owns control flow only. It never implements session
/// storage, context assembly, provider wire translation, tool
/// authorization, or output validation itself; it sequences the
/// components that do and reaches exactly one terminal
/// <see cref="AgentRunOutcome"/> before returning.
/// </para>
/// <para>
/// The <see cref="AgentRunServices"/> parameter is the run's compiled per-agent collaborator bundle. It is
/// supplied here, and never through constructor injection, precisely so that a keyed, scoped implementation
/// cannot have an unkeyed constructor dependency silently replace one definition's selected context assembler,
/// model path, tool invoker, or continuation policy with whatever happens to be the engine-wide default.
/// Constructor injection on an implementation is reserved for genuinely key-independent mechanics such as
/// identifier generators, the clock, and logging.
/// </para>
/// </remarks>
public interface IAgentLoop
{
    /// <summary>Runs one agent loop to settlement.</summary>
    /// <param name="request">The run request.</param>
    /// <param name="services">
    /// The compiled bundle of per-agent collaborators this run uses, resolved by the run-activation boundary for
    /// the exact keyed selection this run's definition named.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the run.</param>
    /// <returns>A task producing the complete result of the run.</returns>
    /// <remarks>
    /// Cancellation follows one rule: the operation may fault with
    /// <see cref="OperationCanceledException"/> only while the run has produced
    /// no durable effect. Once the run has committed at least one message, an
    /// implementation must settle with a typed <see cref="RunCancelled"/>
    /// outcome whose <see cref="AgentLoopResult.NewMessages"/> and
    /// <see cref="AgentLoopResult.FinalVersion"/> report exactly what was
    /// committed, so a caller never loses track of durable state because its
    /// wait was cancelled.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="services"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled before this run committed any message.
    /// </exception>
    public Task<AgentLoopResult> RunAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        CancellationToken cancellationToken = default);
}
