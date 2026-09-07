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
/// This is a deliberately reduced stand-in for the fuller <c>IAgentLoop</c>
/// described by the agent-runtime architecture, which additionally admits
/// queued input through a dedicated I/O coordinator, resolves the model
/// through a neutral catalog/selector, reserves usage against a budget
/// authority, authorizes every tool call through a security authority, and
/// publishes streamed progress through an output publisher. Until those
/// packages exist, a loop implementation resolves its own model from an
/// additively registered set by <see cref="ModelDescriptor.Alias"/>,
/// assumes the caller has already admitted any new input by appending it
/// to the session before starting a run, and performs tool authorization,
/// budget enforcement, and hook dispatch only to the extent those seams
/// exist elsewhere in the composition.
/// </para>
/// <para>
/// An implementation owns control flow only. It never implements session
/// storage, context assembly, provider wire translation, tool
/// authorization, or output validation itself; it sequences the
/// components that do and reaches exactly one terminal
/// <see cref="AgentRunOutcome"/> before returning.
/// </para>
/// </remarks>
public interface IAgentLoop
{
    /// <summary>Runs one agent loop to settlement.</summary>
    /// <param name="request">The run request.</param>
    /// <param name="cancellationToken">A token used to cancel the run.</param>
    /// <returns>A task producing the complete result of the run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<AgentLoopResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default);
}
