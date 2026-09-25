// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Holds one selected endpoint and credential profile pair for a single provider attempt.</summary>
/// <remarks>
/// The lease exposes secret-free snapshots and the keyed credential source selected for the binding. Adapters dispose
/// the lease after credential resolution and send complete.
/// </remarks>
public interface IProviderProfileRuntimeLease: IAsyncDisposable
{
    /// <summary>Gets the selected endpoint-profile snapshot.</summary>
    /// <value>Immutable, secret-free endpoint evidence.</value>
    public ProviderEndpointProfileSnapshot Endpoint { get; }

    /// <summary>Gets the selected credential-profile snapshot.</summary>
    /// <value>Immutable, secret-free credential evidence.</value>
    public ProviderCredentialProfileSnapshot Credential { get; }

    /// <summary>Gets the credential source registered for the credential profile's source key.</summary>
    /// <value>A source resolved from composition; never cached across attempts by the selector.</value>
    public IProviderCredentialSource CredentialSource { get; }
}
