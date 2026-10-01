// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A credential source could not release a lease.</summary>
/// <remarks>
/// Covers grant denial, grant mismatch or replay, a profile the source does not serve, and a secret that could not be
/// resolved. The failure never contains secret material.
/// </remarks>
public sealed record ProviderCredentialUnavailable: ProviderCredentialResolutionResult
{
    /// <summary>Initializes an unavailable result.</summary>
    /// <param name="failure">The normalized failure with a fixed safe message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public ProviderCredentialUnavailable(ProviderFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the normalized failure.</summary>
    public ProviderFailure Failure { get; }
}
