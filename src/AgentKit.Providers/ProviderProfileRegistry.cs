// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Holds endpoint and credential profile snapshots registered at composition time.</summary>
internal sealed class ProviderProfileRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ProviderEndpointProfileReference, ProviderEndpointProfileSnapshot> _endpoints = [];
    private readonly Dictionary<ProviderCredentialProfileReference, ProviderCredentialProfileSnapshot> _credentials = [];

    /// <summary>Registers or replaces one endpoint profile snapshot.</summary>
    /// <param name="snapshot">The snapshot to publish.</param>
    /// <param name="replace">When true, replaces an existing reference; otherwise registration fails on duplicate references.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A duplicate reference is registered without <paramref name="replace"/>.</exception>
    internal void RegisterEndpoint(ProviderEndpointProfileSnapshot snapshot, bool replace)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!replace && _endpoints.ContainsKey(snapshot.Reference))
            {
                throw new InvalidOperationException(
                    $"Endpoint profile '{snapshot.Reference}' is already registered.");
            }

            _endpoints[snapshot.Reference] = snapshot;
        }
    }

    /// <summary>Registers or replaces one credential profile snapshot.</summary>
    /// <param name="snapshot">The snapshot to publish.</param>
    /// <param name="replace">When true, replaces an existing reference; otherwise registration fails on duplicate references.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A duplicate reference is registered without <paramref name="replace"/>.</exception>
    internal void RegisterCredential(ProviderCredentialProfileSnapshot snapshot, bool replace)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (!replace && _credentials.ContainsKey(snapshot.Reference))
            {
                throw new InvalidOperationException(
                    $"Credential profile '{snapshot.Reference}' is already registered.");
            }

            _credentials[snapshot.Reference] = snapshot;
        }
    }

    /// <summary>Gets a snapshot copy of registered endpoint profiles.</summary>
    internal IReadOnlyDictionary<ProviderEndpointProfileReference, ProviderEndpointProfileSnapshot> Endpoints
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<ProviderEndpointProfileReference, ProviderEndpointProfileSnapshot>(_endpoints);
            }
        }
    }

    /// <summary>Gets a snapshot copy of registered credential profiles.</summary>
    internal IReadOnlyDictionary<ProviderCredentialProfileReference, ProviderCredentialProfileSnapshot> Credentials
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<ProviderCredentialProfileReference, ProviderCredentialProfileSnapshot>(_credentials);
            }
        }
    }
}
