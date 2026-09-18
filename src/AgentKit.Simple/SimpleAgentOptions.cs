// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>
/// The behavior of one additional agent hosted by the same engine as the builder's default agent: its own
/// instructions, limits, request settings, and output contract over the shared model, tools, security profile,
/// and session profile.
/// </summary>
/// <remarks>
/// Configure through <c>AgentEngineBuilder.AddAgent(agentId, configure)</c>. The additional agent shares everything
/// the builder's sugar selected (the model alias, the registered tools, the identity, storage, and security), so it
/// differs from the default agent only in what this type describes. Drive it with
/// <c>engine.GetAgentAsync(agentId)</c> and <c>Agent.SendAsync</c>.
/// </remarks>
public sealed class SimpleAgentOptions
{
    /// <summary>Gets or sets the human-readable name used in diagnostics.</summary>
    /// <value><c>"agent"</c> by default.</value>
    public string DisplayName { get; set; } = "agent";

    /// <summary>Gets the system instructions, in call order.</summary>
    public List<string> Instructions { get; } = [];

    /// <summary>Gets or sets the per-run turn limit.</summary>
    /// <value>12 by default; must be positive.</value>
    public int MaxTurns { get; set; } = 12;

    /// <summary>Gets or sets the per-attempt timeout.</summary>
    /// <value>Three minutes by default; must be positive.</value>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Gets or sets the portable request settings applied to every model request.</summary>
    /// <value><see cref="LlmRequestSettings.Default"/> by default; never null.</value>
    public LlmRequestSettings RequestSettings { get; set; } = LlmRequestSettings.Default;

    /// <summary>Gets or sets the structured-output contract every final answer must satisfy, or <see langword="null"/> for free text.</summary>
    public OutputDefinition? Output { get; set; }

    /// <summary>Gets or sets whether the agent is offered every tool registered on the builder's services.</summary>
    /// <value><see langword="true"/> by default; <see langword="false"/> gives the agent no tools.</value>
    public bool IncludeRegisteredTools { get; set; } = true;
}
