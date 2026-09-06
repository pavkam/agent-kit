// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The immutable base for the outcome of resolving a
/// <see cref="ProviderCredential"/> into HTTP authentication for one
/// OpenAI-compatible request, produced by
/// <see cref="OpenAIAuthorizationHeaderFactory"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="OpenAIAuthorizationGranted"/> and
/// <see cref="OpenAIAuthorizationDenied"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Providers.OpenAICompatible can add a third kind.
/// </remarks>
public abstract record OpenAIAuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIAuthorizationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected OpenAIAuthorizationResult()
    {
    }
}
