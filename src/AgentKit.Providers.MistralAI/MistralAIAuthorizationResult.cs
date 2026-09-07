// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

/// <summary>
/// The immutable base for the outcome of resolving a
/// <see cref="ProviderCredential"/> into HTTP authentication for one
/// Mistral AI Chat Completions request, produced by
/// <see cref="MistralAIAuthorizationHeaderFactory"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="MistralAIAuthorizationGranted"/> and
/// <see cref="MistralAIAuthorizationDenied"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Providers.MistralAI can add a third kind.
/// </remarks>
public abstract record MistralAIAuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MistralAIAuthorizationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected MistralAIAuthorizationResult()
    {
    }
}
