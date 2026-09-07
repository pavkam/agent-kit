// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Publishes deterministic catalog snapshots for facade admission tests.</summary>
internal sealed class MutableAgentDefinitionCatalog: IAgentDefinitionCatalog
{
    /// <summary>Initializes the catalog with one non-null current definition.</summary>
    /// <param name="initial">The definition published in the initial snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="initial"/> is <see langword="null"/>.</exception>
    public MutableAgentDefinitionCatalog(AgentDefinition initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        CurrentSnapshot = new AgentCatalogSnapshot(new AgentCatalogVersion(1), [initial]);
    }

    /// <inheritdoc/>
    public bool SupportsDynamicPublication => true;

    /// <inheritdoc/>
    public AgentCatalogSnapshot CurrentSnapshot { get; private set; }

    /// <summary>Gets the number of successful snapshot reads.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc/>
    public ValueTask<AgentCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reads++;
        return ValueTask.FromResult(CurrentSnapshot);
    }

    /// <inheritdoc/>
    public async ValueTask<AgentDefinitionResolution> ResolveAsync(
        AgentId agentId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot.FindDefinition(agentId) is { } definition
            ? new ResolvedAgentDefinition(definition, snapshot.Version)
            : new AgentDefinitionNotFound(agentId);
    }

    /// <summary>Atomically replaces the published snapshot for the next read.</summary>
    /// <param name="version">The non-negative snapshot version to publish.</param>
    /// <param name="definitions">The complete current definitions, including any retained agent.</param>
    public void Publish(long version, params AgentDefinition[] definitions) =>
        CurrentSnapshot = new AgentCatalogSnapshot(new AgentCatalogVersion(version), [.. definitions]);
}
