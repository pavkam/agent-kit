// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>
/// Names the credential profile and audience an HTTP endpoint must bind before
/// it sends a request.
/// </summary>
/// <remarks>
/// This is a reference, not a secret. Tokens, headers, and refresh material
/// stay in the credential source selected by <see cref="CredentialProfileKey"/>.
/// A stdio endpoint omits authentication; an HTTP endpoint that needs
/// credentials carries this reference and still obtains a separate network grant
/// at the transport boundary.
/// </remarks>
public sealed record McpAuthenticationReference
{
    /// <summary>Initializes a non-secret authentication reference.</summary>
    /// <param name="credentialProfileKey">The non-empty credential-profile key.</param>
    /// <param name="audience">The non-empty server audience the credential must be bound to.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="credentialProfileKey"/> or <paramref name="audience"/> is null, empty, or whitespace.
    /// </exception>
    public McpAuthenticationReference(string credentialProfileKey, string audience)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialProfileKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        CredentialProfileKey = credentialProfileKey;
        Audience = audience;
    }

    /// <summary>Gets the credential-profile key. This is not a token.</summary>
    public string CredentialProfileKey { get; }

    /// <summary>Gets the audience the credential must be issued for.</summary>
    public string Audience { get; }
}
