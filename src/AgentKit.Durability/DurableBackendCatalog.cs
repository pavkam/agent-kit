// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Publishes the descriptor of every durable backend registered at composition time.</summary>
/// <remarks>
/// Backends are keyed registrations, so the catalog enumerates them once during construction and retains only their
/// immutable descriptors. Reads are therefore allocation-free and safe for concurrent use, and no read performs a live
/// container lookup or activates a backend.
/// </remarks>
internal sealed class DurableBackendCatalog: IDurableBackendCatalog
{
    private readonly ImmutableArray<DurableBackendDescriptor> _descriptors;

    /// <summary>Snapshots the descriptor of every keyed durable backend in the composition.</summary>
    /// <param name="services">The non-null container holding the keyed backend registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public DurableBackendCatalog(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _descriptors =
        [
            .. services.GetKeyedServices<IDurableExecutionBackend>(KeyedService.AnyKey)
                .Select(static backend => backend.Descriptor),
        ];
    }

    /// <inheritdoc/>
    public ImmutableArray<DurableBackendDescriptor> GetDescriptors() => _descriptors;
}
