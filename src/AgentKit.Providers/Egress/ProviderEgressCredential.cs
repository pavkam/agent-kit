// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

/// <summary>
/// Names the selected credential runtime and the branded authorization scheme <see cref="ProviderEgress"/> uses to obtain
/// a credential-read grant, resolve one credential lease, and apply it to the outgoing request.
/// </summary>
/// <remarks>
/// The adapter selects the runtime lease for the descriptor's captured profile binding (it needs the endpoint snapshot
/// before building the request) and keeps ownership: the adapter disposes the runtime lease after the attempt. Egress
/// never selects a credential source itself and never receives secret material from the adapter.
/// </remarks>
public sealed record ProviderEgressCredential
{
    /// <summary>Initializes an egress credential selection.</summary>
    /// <param name="runtime">The runtime lease selected for the descriptor's endpoint and credential profile binding.</param>
    /// <param name="scheme">The branded provider's verified API-key header shape.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runtime"/> or <paramref name="scheme"/> is null.</exception>
    public ProviderEgressCredential(IProviderProfileRuntimeLease runtime, ProviderAuthorizationScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(scheme);

        Runtime = runtime;
        Scheme = scheme;
    }

    /// <summary>Gets the selected runtime lease; the adapter owns and disposes it.</summary>
    public IProviderProfileRuntimeLease Runtime { get; init; }

    /// <summary>Gets the branded provider's verified API-key header shape.</summary>
    public ProviderAuthorizationScheme Scheme { get; init; }
}
