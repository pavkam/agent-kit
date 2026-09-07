// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// The immutable base for the outcome of resolving a
/// <see cref="ProviderCredential"/> into HTTP authentication for one
/// Cohere v2 Chat request, produced by
/// <see cref="CohereAuthorizationHeaderFactory"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="CohereAuthorizationGranted"/> and
/// <see cref="CohereAuthorizationDenied"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Providers.Cohere can add a third kind.
/// </remarks>
public abstract record CohereAuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CohereAuthorizationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected CohereAuthorizationResult()
    {
    }
}
