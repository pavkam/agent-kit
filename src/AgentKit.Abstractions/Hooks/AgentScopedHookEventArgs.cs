// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Base for hook event arguments whose dispatching stage has already
/// established the agent, and optionally the session, the invocation
/// relates to.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AgentHookEventArgs"/> itself carries only facts that exist for
/// every dispatch at every stage, because engine construction can precede an
/// <see cref="AgentKit.AgentId"/>, agent registration can precede a
/// <see cref="AgentKit.SessionId"/>, and admission can precede a
/// <see cref="RunId"/>. A hook point whose stage has already
/// resolved the agent — every first-party point today does — derives from
/// this intermediate class instead of adding its own duplicate
/// <see cref="AgentId"/>/<see cref="SessionId"/> storage, and instead of
/// requiring every future hook point (including ones that fire before an
/// agent is known) to carry identities it cannot truthfully have.
/// </para>
/// <para>
/// This type carries no mutable state of its own and is safe to share
/// across threads without synchronization, matching the base class.
/// </para>
/// </remarks>
public abstract class AgentScopedHookEventArgs: AgentHookEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="AgentScopedHookEventArgs"/> class.</summary>
    /// <param name="dispatch">The point identity, dispatch identity, causality, and timing facts for this dispatch.</param>
    /// <param name="agentId">The agent this hook invocation occurred for.</param>
    /// <param name="sessionId">The session this hook invocation relates to, when applicable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatch"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="agentId"/> or a present <paramref name="sessionId"/> is default.
    /// </exception>
    protected AgentScopedHookEventArgs(
        HookDispatchMetadata dispatch,
        AgentId agentId,
        SessionId? sessionId)
        : base(dispatch)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        if (sessionId is { } session)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(session, default, nameof(sessionId));
        }

        AgentId = agentId;
        SessionId = sessionId;
    }

    /// <summary>Gets the agent this hook invocation occurred for.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the session this hook invocation relates to, when applicable.</summary>
    public SessionId? SessionId { get; }
}
