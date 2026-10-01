// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Publishes every agent definition the engine's catalog hosts as a delegation target.</summary>
/// <remarks>Discovery is metadata, not authority: publishing a target grants nothing. Each call captures the catalog's current snapshot independently, so a later publication never changes an earlier capture.</remarks>
/// <param name="catalog">The non-null engine-wide definition catalog.</param>
internal sealed class LocalAgentDelegationTargetProvider(IAgentDefinitionCatalog catalog): IDelegationTargetProvider
{
    /// <summary>Gets the provider identity recorded on every target it publishes.</summary>
    internal static ComponentId Source { get; } = new("agentkit.goals.local-agents");

    /// <inheritdoc/>
    public async ValueTask<DelegationTargetSnapshot> DiscoverAsync(DelegationDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var snapshot = await catalog.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return new DelegationTargetSnapshot(
            Source,
            [.. snapshot.Definitions.Select(static definition => new DelegationTarget(definition.Id, definition.Revision, Source, []))]);
    }
}
