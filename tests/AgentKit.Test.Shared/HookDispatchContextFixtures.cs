// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds a real, empty hook dispatch context for tests that only need to observe one being forwarded.</summary>
public static class HookDispatchContextFixtures
{
    /// <summary>Creates a context over an empty catalog and a lease that resolves no hooks.</summary>
    /// <param name="point">The hook point the dispatch metadata names.</param>
    /// <param name="correlation">The causal operation the dispatch is stamped with.</param>
    /// <returns>A dispatch context whose activation is not tracked for disposal; the lease holds no resources.</returns>
    public static async Task<HookDispatchContext> CreateAsync(HookPointId point, OperationCorrelation correlation)
    {
        ArgumentNullException.ThrowIfNull(correlation);

        var catalog = new HookCatalogSnapshot(HookRegistrationDescriptors.DefaultProfileKey, new HookCatalogVersion("test"), []);
        var lease = await new StaticHookInstanceFactory(new Dictionary<HookRegistrationId, object>())
            .CreateAsync(catalog, CancellationToken.None)
            .ConfigureAwait(false);
        return new HookActivationScope(catalog, lease).CreateDispatch(new HookDispatchMetadata(
            point,
            new HookDispatchId(Guid.NewGuid()),
            correlation,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(30)));
    }
}
