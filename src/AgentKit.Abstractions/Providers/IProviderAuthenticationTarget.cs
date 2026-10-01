// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Describes one fully prepared outgoing provider request to an <see cref="IProviderCredentialLease"/> and receives the
/// authentication headers the lease produces.
/// </summary>
/// <remarks>
/// The request description is final when the target is created: the body is already frozen, so request-signing
/// credentials (for example AWS Signature Version 4) sign exactly the bytes sent. A lease may call
/// <see cref="SetHeader"/> any number of times and never reads the headers back.
/// </remarks>
public interface IProviderAuthenticationTarget
{
    /// <summary>Gets the provider the request is sent to, used for safe diagnostics.</summary>
    public ProviderId ProviderId { get; }

    /// <summary>Gets the verified API-key header shape the branded provider declares for this request.</summary>
    public ProviderAuthorizationScheme Scheme { get; }

    /// <summary>Gets the current instant from the injected clock of the boundary that applies the credential.</summary>
    /// <value>
    /// The instant a lease uses to judge token expiry and to stamp request signatures, so a lease owns no clock of its
    /// own and tests control time through the same <see cref="TimeProvider"/> as the rest of the attempt.
    /// </value>
    public DateTimeOffset UtcNow { get; }

    /// <summary>Gets the HTTP method of the request in upper-case token form.</summary>
    public string Method { get; }

    /// <summary>Gets the absolute request address.</summary>
    public Uri RequestUri { get; }

    /// <summary>Gets the content type of the frozen body, or <see langword="null"/> when the request has no body.</summary>
    public string? ContentType { get; }

    /// <summary>Gets the frozen request body bytes, empty when the request has no body.</summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary>Sets one authentication header, replacing any existing header of the same name.</summary>
    /// <param name="name">The non-empty header name.</param>
    /// <param name="value">The non-empty header value; it is secret material and must not be logged.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="value"/> is null, empty, or whitespace.</exception>
    public void SetHeader(string name, string value);
}
