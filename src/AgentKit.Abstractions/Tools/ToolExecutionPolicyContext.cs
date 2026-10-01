// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies the batch an <see cref="IToolExecutionPolicy"/> is planning.</summary>
/// <remarks>The context carries identities and the planning instant only. It carries no authority and no mutable run state.</remarks>
public sealed record ToolExecutionPolicyContext
{
    /// <summary>Initializes a planning context.</summary>
    /// <param name="agentId">The nondefault owning agent.</param>
    /// <param name="sessionId">The nondefault owning session.</param>
    /// <param name="runId">The nondefault active run.</param>
    /// <param name="catalogVersion">The nondefault catalog version every planned call resolved against.</param>
    /// <param name="plannedAt">The planning instant from the injected clock.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity or <paramref name="catalogVersion"/> is default.</exception>
    public ToolExecutionPolicyContext(
        AgentId agentId,
        SessionId sessionId,
        RunId runId,
        ToolCatalogVersion catalogVersion,
        DateTimeOffset plannedAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(catalogVersion, default);
        AgentId = agentId;
        SessionId = sessionId;
        RunId = runId;
        CatalogVersion = catalogVersion;
        PlannedAt = plannedAt;
    }

    /// <summary>Gets the owning agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the owning session.</summary>
    public SessionId SessionId { get; }

    /// <summary>Gets the active run.</summary>
    public RunId RunId { get; }

    /// <summary>Gets the catalog version the planned calls resolved against.</summary>
    public ToolCatalogVersion CatalogVersion { get; }

    /// <summary>Gets the planning instant.</summary>
    public DateTimeOffset PlannedAt { get; }
}
