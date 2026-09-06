// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one provider-neutral credential resolved at send
/// time by a provider credential source.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="ApiKeyProviderCredential"/> and
/// <see cref="OAuthTokenProviderCredential"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a third kind; a concrete provider package
/// that needs a different authentication shape defines its own
/// provider-specific credential type and its own provider credential source
/// implementation rather than extending this hierarchy.
/// </para>
/// <para>
/// A credential value is never stored in a <see cref="ModelDescriptor"/>,
/// message, configuration snapshot, event, exception, or replay log. It is
/// resolved fresh from a provider credential source immediately before it
/// is applied to a request, and it is never logged.
/// </para>
/// </remarks>
public abstract record ProviderCredential
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCredential"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected ProviderCredential()
    {
    }
}
