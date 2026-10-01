// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Merges every registered target provider's independent capture in provider registration order.</summary>
/// <remarks>The catalog never chooses between providers that publish the same agent; the selector resolves or rejects that ambiguity.</remarks>
/// <param name="providers">The additive providers in registration order.</param>
internal sealed class DefaultDelegationTargetCatalog(IEnumerable<IDelegationTargetProvider> providers): IDelegationTargetCatalog
{
    private readonly ImmutableArray<IDelegationTargetProvider> _providers = [.. providers];

    /// <inheritdoc/>
    public async ValueTask<DelegationTargetCatalogSnapshot> CaptureAsync(DelegationDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var targets = ImmutableArray.CreateBuilder<DelegationTarget>();
        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await provider.DiscoverAsync(request, cancellationToken).ConfigureAwait(false);
            targets.AddRange(snapshot.Targets);
        }

        return new DelegationTargetCatalogSnapshot(targets.ToImmutable());
    }
}
