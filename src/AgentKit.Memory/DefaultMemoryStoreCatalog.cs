// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Lists the descriptors of every memory store registered under a key in the container.</summary>
/// <remarks>Discovery only: the catalog grants nothing and selects nothing. It enumerates keyed registrations, so stores registered by leaf packages appear without the runtime knowing their concrete types. It is the typed boundary over the container; no other component receives the service provider.</remarks>
/// <param name="services">The container holding the keyed store registrations.</param>
internal sealed class DefaultMemoryStoreCatalog(IServiceProvider services): IMemoryStoreCatalog
{
    /// <inheritdoc/>
    public ImmutableArray<MemoryStoreDescriptor> GetDescriptors() =>
        [.. services.GetKeyedServices<IMemoryStore>(KeyedService.AnyKey).Select(static store => store.Descriptor).OrderBy(static descriptor => descriptor.Key.Value, StringComparer.Ordinal)];
}
