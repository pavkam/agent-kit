// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one protected semantic provider operation to its exact address, identity, causal correlation, and captured authorization evidence.</summary>
/// <remarks>
/// This immutable value is authorization evidence, not a security grant. Provider execution and lower effecting boundaries
/// must still obtain and consume their own bounded grants immediately before acting. Before-run, in-run, and after-run
/// correlations remain distinct and are preserved exactly; a causal run identity never becomes an active run implicitly.
/// </remarks>
public sealed record ProtectedSemanticOperationContext
{
    /// <summary>Initializes one internally consistent protected semantic operation context.</summary>
    /// <param name="agentId">The nondefault agent performing the operation.</param>
    /// <param name="sessionId">The nondefault session when the operation truthfully belongs to one; otherwise <see langword="null"/>.</param>
    /// <param name="conversationId">The nondefault conversation when one has been established; otherwise <see langword="null"/>.</param>
    /// <param name="identity">The complete authenticated execution identity.</param>
    /// <param name="correlation">The exact before-run, in-run, or after-run causal operation.</param>
    /// <param name="authorization">The captured authorization evidence that must exactly match the agent, session, identity, and correlation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/>, <paramref name="correlation"/>, or <paramref name="authorization"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/>, or a present <paramref name="sessionId"/> or <paramref name="conversationId"/>, is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="authorization"/> does not exactly bind the supplied agent, session, identity, and correlation.</exception>
    public ProtectedSemanticOperationContext(
        AgentId agentId,
        SessionId? sessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        OperationCorrelation correlation,
        SecurityAuthorizationContext authorization)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default, nameof(agentId));
        if (sessionId is { } presentSessionId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(presentSessionId, default, nameof(sessionId));
        }

        if (conversationId is { } presentConversationId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(presentConversationId, default, nameof(conversationId));
        }

        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentException.ThrowIfInvalidOperationAuthorization(
            identity, agentId, sessionId, correlation, authorization);

        AgentId = agentId;
        SessionId = sessionId;
        ConversationId = conversationId;
        Identity = identity;
        Correlation = correlation;
        Authorization = authorization;
    }

    /// <summary>Gets the agent performing the operation.</summary>
    /// <value>The nondefault agent identity exactly matched by <see cref="Authorization"/>.</value>
    public AgentId AgentId { get; }

    /// <summary>Gets the session when the operation truthfully belongs to one.</summary>
    /// <value>The nondefault session exactly matched by <see cref="Authorization"/>, or <see langword="null"/>.</value>
    public SessionId? SessionId { get; }

    /// <summary>Gets the established conversation associated with the operation.</summary>
    /// <value>A nondefault conversation identity, or <see langword="null"/> when no conversation has been established.</value>
    public ConversationId? ConversationId { get; }

    /// <summary>Gets the complete authenticated execution identity.</summary>
    /// <value>The immutable identity exactly matched by <see cref="Authorization"/>.</value>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the exact causal operation correlation.</summary>
    /// <value>The immutable before-run, in-run, or after-run correlation exactly matched by <see cref="Authorization"/>.</value>
    public OperationCorrelation Correlation { get; }

    /// <summary>Gets the captured authorization evidence for this semantic operation.</summary>
    /// <value>Immutable evidence exactly bound to the operation address, identity, and correlation.</value>
    public SecurityAuthorizationContext Authorization { get; }
}
