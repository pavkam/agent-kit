// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI;

/// <summary>
/// The immutable base for the outcome of resolving a
/// <see cref="ProviderCredential"/> into HTTP authentication for one Azure
/// OpenAI GA v1 Chat Completions request, produced by
/// <see cref="AzureOpenAIAuthorizationHeaderFactory"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="AzureOpenAIAuthorizationGranted"/> and
/// <see cref="AzureOpenAIAuthorizationDenied"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Providers.AzureOpenAI can add a third kind.
/// </remarks>
public abstract record AzureOpenAIAuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIAuthorizationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected AzureOpenAIAuthorizationResult()
    {
    }
}
