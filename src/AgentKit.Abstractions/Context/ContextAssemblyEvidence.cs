// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the atomic definition, identity, history, authorization, and configuration evidence for one context assembly.</summary>
/// <remarks>Conversation correlation is retained by <see cref="HistoryView.SourceCursor"/>; the current authorization scope exposes no conversation coordinate to cross-check.</remarks>
public sealed record ContextAssemblyEvidence
{
    /// <summary>Creates one locally coherent evidence capture.</summary>
    /// <param name="agent">The exact immutable agent definition.</param>
    /// <param name="identity">The authenticated execution identity.</param>
    /// <param name="history">The repaired history view and exact source cursor.</param>
    /// <param name="authorization">The captured authorization selection and scope.</param>
    /// <param name="configuration">The exact effective configuration snapshot.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="ArgumentException">Agent, session, identity, definition revision, or configuration revision evidence is inconsistent.</exception>
    public ContextAssemblyEvidence(AgentDefinition agent, ExecutionIdentity identity, HistoryView history, SecurityAuthorizationContext authorization, EffectiveConfigurationSnapshot configuration)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNotEqual(history.SourceCursor.AgentId, agent.Id, nameof(history));
        ArgumentException.ThrowIfNotEqual(authorization.Identity, identity, nameof(identity));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, agent.Id, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, history.SourceCursor.SessionId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.AgentDefinitionRevision, agent.Revision, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.ConfigurationVersion, configuration.Version, nameof(configuration));
        Agent = agent;
        Identity = identity;
        History = history;
        Authorization = authorization;
        Configuration = configuration;
    }

    /// <summary>Gets the captured agent definition.</summary><value>The exact immutable definition.</value>
    public AgentDefinition Agent { get; }
    /// <summary>Gets the authenticated execution identity.</summary><value>The complete trusted-ingress identity evidence.</value>
    public ExecutionIdentity Identity { get; }
    /// <summary>Gets the captured repaired history view.</summary><value>The exact view and source cursor.</value>
    public HistoryView History { get; }
    /// <summary>Gets captured authorization evidence.</summary><value>The exact selection, scope, identity, and revisions.</value>
    public SecurityAuthorizationContext Authorization { get; }
    /// <summary>Gets the captured effective configuration.</summary><value>The exact immutable configuration snapshot.</value>
    public EffectiveConfigurationSnapshot Configuration { get; }
}
