// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Http;

/// <summary>
/// The credential could not be resolved into usable authentication, such as
/// an expired OAuth token, a credential kind the provider does not accept,
/// or an unrecognized credential kind.
/// </summary>
/// <remarks>
/// A denial is discovered before any network request is sent, so the
/// caller fails the attempt without a wasted round trip and without ever
/// sending an authentication header known in advance to be rejected. The
/// carried <see cref="Failure"/> never contains credential material; its
/// message names only the credential's CLR type and the provider identity.
/// </remarks>
public sealed record ProviderAuthorizationDenied: ProviderAuthorizationResult
{
    /// <summary>Initializes a new instance of the <see cref="ProviderAuthorizationDenied"/> record.</summary>
    /// <param name="failure">
    /// The normalized failure, whose <see cref="ProviderFailure.Kind"/> is
    /// <see cref="ProviderFailureKind.Authentication"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public ProviderAuthorizationDenied(ProviderFailure failure)
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
