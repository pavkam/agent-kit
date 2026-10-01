// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Configures the one composed agent a <see cref="DefaultConversationSession"/> drives turns against.</summary>
/// <remarks>
/// This is a mutable options type bound through <see cref="IOptions{TOptions}"/>; configure it during composition
/// with <c>AddConversationSession</c>, which validates every required value before the session can be resolved.
/// The pinned <see cref="Agent"/> and <see cref="Configuration"/> are the single source of the agent identity, security
/// profile, definition revision, configuration version, turn limits, and output contract, so this type carries no
/// second copy of any of them. <see cref="DefaultConversationSession"/> copies <see cref="ToolPresentationBindings"/>
/// into an immutable snapshot when constructed, so mutating this options instance afterward does not change an
/// already-resolved session.
/// </remarks>
public sealed class ConversationSessionOptions
{
    /// <summary>Gets or sets the exact immutable agent definition every turn runs against.</summary>
    /// <value>The admitted definition; required.</value>
    public AgentDefinition? Agent { get; set; }

    /// <summary>Gets or sets the exact effective configuration captured with <see cref="Agent"/>.</summary>
    /// <value>An immutable snapshot whose version matches the session profile's configuration fingerprint; required.</value>
    public EffectiveConfigurationSnapshot? Configuration { get; set; }

    /// <summary>Gets or sets the authenticated identity every turn runs under.</summary>
    /// <value>A non-null identity; required.</value>
    public ExecutionIdentity? Identity { get; set; }

    /// <summary>Gets or sets the session-store routing and behavior profile every turn loads and appends through.</summary>
    /// <value>A non-null snapshot selected by <see cref="Agent"/>; required.</value>
    public SessionProfileSnapshot? SessionProfile { get; set; }

    /// <summary>Gets optional exact descriptor bindings used only to present calls and projected results.</summary>
    /// <value>
    /// Empty by default. Every binding must have a unique tool identity and advertised alias; the session validates and
    /// snapshots the bindings during construction.
    /// </value>
    public IList<ConversationToolPresentationBinding> ToolPresentationBindings { get; } = [];
}
