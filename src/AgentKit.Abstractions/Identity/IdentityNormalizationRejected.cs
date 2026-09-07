// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a typed rejection during issuer mapping or claim normalization.</summary>
public sealed record IdentityNormalizationRejected: IdentityNormalizationResult
{
    /// <summary>Initializes a rejected normalization result.</summary>
    /// <param name="failure">The safe identity failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public IdentityNormalizationRejected(IdentityFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Failure = failure;
    }

    /// <summary>Gets the normalization failure.</summary>
    public IdentityFailure Failure { get; }
}
