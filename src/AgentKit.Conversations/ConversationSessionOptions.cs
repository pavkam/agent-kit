// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Configures the one composed agent a <see cref="DefaultConversationSession"/> drives turns against.</summary>
/// <remarks>
/// This is a mutable options type bound through <see cref="IOptions{TOptions}"/>; configure it during composition
/// with <c>AddConversationSession</c>, which validates every required value before the session can be resolved.
/// <see cref="DefaultConversationSession"/> copies <see cref="Instructions"/> and <see cref="Tools"/> into an
/// immutable snapshot when constructed, so mutating this options instance afterward does not change an
/// already-resolved session.
/// </remarks>
public sealed class ConversationSessionOptions
{
    /// <summary>Gets or sets the identity of the agent this session drives turns for.</summary>
    /// <value>A non-default identity; required.</value>
    public AgentId AgentId { get; set; }

    /// <summary>Gets or sets the authenticated identity every turn runs under.</summary>
    /// <value>A non-null identity; required.</value>
    public ExecutionIdentity? Identity { get; set; }

    /// <summary>Gets or sets the named security profile every turn captures authorization against.</summary>
    /// <value>A non-default profile key; required.</value>
    public SecurityProfileKey SecurityProfileKey { get; set; }

    /// <summary>Gets or sets the agent-definition revision this session's security and session profiles are published for.</summary>
    /// <value>A non-default revision; required.</value>
    public AgentDefinitionRevision AgentDefinitionRevision { get; set; }

    /// <summary>Gets or sets the effective-configuration revision this session's profiles are published for.</summary>
    /// <value>A non-default revision; required.</value>
    public ConfigurationVersion ConfigurationVersion { get; set; }

    /// <summary>Gets or sets the session-store routing and behavior profile every turn loads and appends through.</summary>
    /// <value>A non-null snapshot; required.</value>
    public SessionProfileSnapshot? SessionProfile { get; set; }

    /// <summary>Gets or sets the model aliases and precedence a run may select from.</summary>
    /// <value>A non-null policy; required.</value>
    public ModelSelectionPolicy? ModelSelectionPolicy { get; set; }

    /// <summary>Gets or sets the model capability and limit requirements a run applies.</summary>
    /// <value><see cref="ModelRequirements.None"/> by default.</value>
    public ModelRequirements ModelRequirements { get; set; } = ModelRequirements.None;

    /// <summary>Gets the instruction messages included with every run, in order.</summary>
    /// <value>Empty by default. Typically one <c>SystemMessage</c> carrying the agent's system prompt.</value>
    public IList<AgentMessage> Instructions { get; } = [];

    /// <summary>Gets the tool definitions advertised to the model on every run.</summary>
    /// <value>Empty by default; a host resolving tools from a DI-registered catalog adds their converted definitions here.</value>
    public IList<LlmToolDefinition> Tools { get; } = [];

    /// <summary>Gets or sets how the model may select among <see cref="Tools"/>.</summary>
    /// <value><see cref="LlmToolChoice.Auto"/> by default.</value>
    public LlmToolChoice ToolChoice { get; set; } = LlmToolChoice.Auto;

    /// <summary>Gets or sets the request-level sampling and limit settings applied to every run.</summary>
    /// <value><see cref="LlmRequestSettings.Default"/> by default.</value>
    public LlmRequestSettings RequestSettings { get; set; } = LlmRequestSettings.Default;

    /// <summary>Gets or sets the maximum tool-calling turns one run may take before it stops.</summary>
    /// <value>A positive count; twelve by default.</value>
    public int MaxTurns { get; set; } = 12;

    /// <summary>Gets or sets the maximum wall-clock time one run attempt may take.</summary>
    /// <value>A positive duration; three minutes by default.</value>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(3);
}
