// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the run identities and authorization used to build a legacy catalog capture.</summary>
public sealed record RunToolCatalogCaptureRequest
{
    /// <summary>Initializes run catalog capture evidence.</summary>
    /// <param name="agentId">The owning agent.</param>
    /// <param name="sessionId">The owning session.</param>
    /// <param name="runId">The active run.</param>
    /// <param name="authorization">The captured authorization for tool execution.</param>
    /// <param name="toolsets">Authored toolset selections for discovery capture; empty uses the legacy singleton catalog path.</param>
    /// <param name="configuration">Effective configuration for discovery; required when <paramref name="toolsets"/> is non-empty.</param>
    /// <param name="modelCapabilities">Model capability snapshot for schema preflight; required when <paramref name="toolsets"/> is non-empty.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="authorization"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="toolsets"/> is non-empty but configuration or capabilities is null.</exception>
    public RunToolCatalogCaptureRequest(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        SecurityAuthorizationContext authorization,
        ImmutableArray<ToolsetReference> toolsets = default,
        EffectiveConfigurationSnapshot? configuration = null,
        ModelCapabilities? modelCapabilities = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfContainsNull(toolsets);
        if (!toolsets.IsDefaultOrEmpty)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(modelCapabilities);
        }

        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        Authorization = authorization;
        Toolsets = toolsets.IsDefault ? [] : toolsets;
        Configuration = configuration;
        ModelCapabilities = modelCapabilities;
    }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the owning session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the active run.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the captured authorization.</summary>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets authored toolset selections for run-bound discovery.</summary>
    public ImmutableArray<ToolsetReference> Toolsets { get; }

    /// <summary>Gets the effective configuration used when discovering toolsets.</summary>
    public EffectiveConfigurationSnapshot? Configuration { get; }

    /// <summary>Gets the model capabilities used during catalog preflight.</summary>
    public ModelCapabilities? ModelCapabilities { get; }
}
