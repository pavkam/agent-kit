// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

/// <summary>
/// The immutable base for the outcome of resolving a
/// <see cref="ProviderCredential"/> into HTTP authentication for one
/// Anthropic Messages request, produced by
/// <see cref="AnthropicAuthorizationHeaderFactory"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="AnthropicAuthorizationGranted"/> and
/// <see cref="AnthropicAuthorizationDenied"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Providers.Anthropic can add a third kind.
/// </remarks>
public abstract record AnthropicAuthorizationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AnthropicAuthorizationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected AnthropicAuthorizationResult()
    {
    }
}
