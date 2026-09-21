// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable evidence one context contributor receives for a single evaluation.</summary>
public sealed record ContextContributionRequest
{
    /// <summary>Initializes contributor request evidence.</summary>
    /// <param name="agent">The pinned agent definition.</param>
    /// <param name="sessionId">The session whose branch supplies history.</param>
    /// <param name="conversationId">The optional conversation correlation for the session.</param>
    /// <param name="identity">The authenticated execution identity for this run.</param>
    /// <param name="runId">The active run identifier.</param>
    /// <param name="turnId">The active turn identifier.</param>
    /// <param name="modelRequestId">The model request being assembled.</param>
    /// <param name="model">The selected conversational model descriptor.</param>
    /// <param name="history">The validated history view available to contributors.</param>
    /// <param name="authorization">The captured authorization evidence for protected reads.</param>
    /// <param name="configuration">The effective configuration snapshot for this assembly.</param>
    /// <exception cref="ArgumentNullException">Any reference-type dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Any required identity is default.</exception>
    public ContextContributionRequest(
        AgentDefinition agent,
        SessionId sessionId,
        ConversationId? conversationId,
        ExecutionIdentity identity,
        RunId runId,
        TurnId turnId,
        ModelRequestId modelRequestId,
        ModelDescriptor model,
        HistoryView history,
        SecurityAuthorizationContext authorization,
        EffectiveConfigurationSnapshot configuration)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(turnId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(configuration);

        Agent = agent;
        SessionId = sessionId;
        ConversationId = conversationId;
        Identity = identity;
        RunId = runId;
        TurnId = turnId;
        ModelRequestId = modelRequestId;
        Model = model;
        History = history;
        Authorization = authorization;
        Configuration = configuration;
    }

    /// <summary>Gets the pinned agent definition.</summary>
    public AgentDefinition Agent { get; }

    /// <summary>Gets the session whose branch supplies history.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the optional conversation correlation for the session.</summary>
    public ConversationId? ConversationId { get; }

    /// <summary>Gets the authenticated execution identity for this run.</summary>
    public ExecutionIdentity Identity { get; }

    /// <summary>Gets the active run identifier.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the active turn identifier.</summary>
    public TurnId TurnId { get; }

    /// <summary>Gets the model request being assembled.</summary>
    public ModelRequestId ModelRequestId { get; }

    /// <summary>Gets the selected conversational model descriptor.</summary>
    public ModelDescriptor Model { get; }

    /// <summary>Gets the validated history view available to contributors.</summary>
    public HistoryView History { get; }

    /// <summary>Gets the captured authorization evidence for protected reads.</summary>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the effective configuration snapshot for this assembly.</summary>
    public EffectiveConfigurationSnapshot Configuration { get; }
}
