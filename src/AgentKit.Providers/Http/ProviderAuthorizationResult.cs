// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

/// <summary>
/// The immutable base for the outcome of resolving a provider-neutral
/// <see cref="ProviderCredential"/> into one HTTP authentication header for
/// one provider request, produced by
/// <see cref="ProviderAuthorizationHeaderFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="ProviderAuthorizationGranted"/> and
/// <see cref="ProviderAuthorizationDenied"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Providers can add a third kind, and a consumer can switch
/// exhaustively over the two cases.
/// </para>
/// <para>
/// The hierarchy is shared by every first-party HTTP provider adapter.
/// The only per-provider variation is which header name and value prefix
/// carry a static API key, and whether API keys are accepted at all; that
/// variation is expressed through <see cref="ProviderAuthorizationScheme"/>
/// rather than through a per-package copy of these result types.
/// </para>
/// </remarks>
public abstract record ProviderAuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderAuthorizationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected ProviderAuthorizationResult()
    {
    }
}
