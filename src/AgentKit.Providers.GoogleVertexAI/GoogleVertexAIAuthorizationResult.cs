// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// The immutable base for the outcome of resolving a
/// <see cref="ProviderCredential"/> into HTTP authentication for one
/// Vertex AI generateContent request, produced by
/// <see cref="GoogleVertexAIAuthorizationHeaderFactory"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="GoogleVertexAIAuthorizationGranted"/> and
/// <see cref="GoogleVertexAIAuthorizationDenied"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Providers.GoogleVertexAI can add a third kind.
/// </remarks>
public abstract record GoogleVertexAIAuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleVertexAIAuthorizationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected GoogleVertexAIAuthorizationResult()
    {
    }
}
