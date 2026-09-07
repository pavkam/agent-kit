// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// The credential could not be resolved into usable authentication, such as
/// an expired OAuth access token or an unsupported credential kind (Vertex
/// AI has no API-key authentication mechanism; a supplied
/// <see cref="ApiKeyProviderCredential"/> is always denied).
/// </summary>
/// <remarks>
/// A denial is discovered before any network request is sent, so the
/// caller fails the attempt without a wasted round trip and without ever
/// sending an authentication header known in advance to be rejected.
/// </remarks>
public sealed record GoogleVertexAIAuthorizationDenied: GoogleVertexAIAuthorizationResult
{
    /// <summary>Initializes a new instance of the <see cref="GoogleVertexAIAuthorizationDenied"/> record.</summary>
    /// <param name="failure">
    /// The normalized failure, whose <see cref="ProviderFailure.Kind"/> is
    /// <see cref="ProviderFailureKind.Authentication"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public GoogleVertexAIAuthorizationDenied(ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>
    /// Gets the normalized failure, whose <see cref="ProviderFailure.Kind"/>
    /// is <see cref="ProviderFailureKind.Authentication"/>.
    /// </summary>
    public ProviderFailure Failure { get; init; }
}
