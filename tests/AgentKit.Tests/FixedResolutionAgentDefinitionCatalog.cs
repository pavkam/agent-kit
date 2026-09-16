// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Returns one fixed resolution outcome regardless of the requested identity.</summary>
internal sealed class FixedResolutionAgentDefinitionCatalog(AgentDefinitionResolution resolution): IAgentDefinitionCatalog
{
    /// <inheritdoc/>
    public bool SupportsDynamicPublication => false;

    /// <inheritdoc/>
    public AgentCatalogSnapshot CurrentSnapshot { get; } = new(new AgentCatalogVersion(1), []);

    /// <inheritdoc/>
    public ValueTask<AgentCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(CurrentSnapshot);

    /// <inheritdoc/>
    public ValueTask<AgentDefinitionResolution> ResolveAsync(AgentId agentId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(resolution);
}
