// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Optional composition hook that binds the registered conversation session to an engine open request.
/// </summary>
/// <remarks>
/// The conversations package registers the default implementation when
/// <c>AddConversationSession</c> is composed together with <c>AddAgentKit</c>. The facade references only this
/// contract so it never depends on the conversations package.
/// </remarks>
public interface IConversationEngineHost
{
    /// <summary>Opens or validates the conversation session for one agent and identity.</summary>
    /// <param name="request">The agent, identity, and optional existing session.</param>
    /// <param name="cancellationToken">Cancels the open before any durable binding commits.</param>
    /// <returns>An opened binding or a safe rejection.</returns>
    public ValueTask<AgentConversationOpenResult> OpenAsync(
        AgentConversationOpenRequest request,
        CancellationToken cancellationToken = default);
}
