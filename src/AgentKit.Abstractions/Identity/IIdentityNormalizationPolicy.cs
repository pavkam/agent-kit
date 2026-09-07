// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Applies one deterministically ordered normalization rule to an issuer-mapped candidate.</summary>
public interface IIdentityNormalizationPolicy
{
    /// <summary>Normalizes a candidate without broadening its trusted issuer provenance.</summary>
    /// <param name="request">The assertion and current candidate.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs normalization.</param>
    /// <returns>An updated immutable candidate or typed rejection.</returns>
    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityNormalizationRequest request, CancellationToken cancellationToken = default);
}
