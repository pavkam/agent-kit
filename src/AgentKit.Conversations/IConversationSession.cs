// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Drives one ongoing conversation: session creation, message admission, and the agent-loop run in one call.</summary>
/// <remarks>
/// <para>
/// Composing a single working conversational turn otherwise requires an application to call
/// <c>ISessionCoordinator</c> to create and load a session, capture authorization through
/// <c>ISecurityProfileSelector</c> twice (once to admit the user's message, once to authorize the run), append a
/// <c>MessageSessionEntry</c> itself, build an <c>AgentRunRequest</c> from its own instructions and tools, and run
/// <c>IAgentLoop</c> directly — all of it bypassing the <c>AgentEngine</c>/<c>Agent</c> facade, which has no method
/// to admit a message into a session before starting a run. <see cref="IConversationSession"/> performs all of
/// that consistently for the common case of one long-lived, single-branch conversation against one composed agent.
/// </para>
/// <para>
/// This is not a replacement for <c>AgentEngine</c>: it does not host a catalog of several agent definitions,
/// does not version or admit definitions, and does not implement the durable, queue-backed input admission
/// <c>AgentKit.IO</c> provides for multi-writer or distributed hosts. It is the direct, in-process composition an
/// application reaches for when it owns its own <see cref="IServiceProvider"/> and wants one conversation with one
/// agent, such as a terminal, desktop, or single-tenant service host.
/// </para>
/// </remarks>
public interface IConversationSession
{
    /// <summary>Submits one user message, driving session creation, admission, and a full agent-loop run.</summary>
    /// <param name="userText">The nonblank user-authored message text.</param>
    /// <param name="cancellationToken">Cancels the pending turn.</param>
    /// <returns>The committed activity for this turn, or a single explanatory event when admission itself failed.</returns>
    /// <exception cref="ArgumentException"><paramref name="userText"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="InvalidOperationException">Session creation or authorization capture did not succeed.</exception>
    /// <remarks>
    /// Calls to one <see cref="IConversationSession"/> instance are serialized: a call that arrives while a prior
    /// call is still running awaits that prior call's completion rather than interleaving with it, because both
    /// share one session and one active branch. The first call lazily creates the underlying session; every
    /// later call reuses it.
    /// </remarks>
    public Task<ConversationTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default);
}
