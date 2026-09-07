// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Applies one deterministically ordered narrowing rule to an issuer-mapped candidate.</summary>
/// <remarks>Each policy may remove claims, lower assurance, or reject the candidate. It must preserve the mapped tenant, principal, subject kind, authentication evidence, identity version, and complete delegation chain. Narrowing is evaluated against the candidate produced by the immediately preceding policy.</remarks>
public interface IIdentityNormalizationPolicy
{
    /// <summary>Narrows the current candidate without replacing authenticated identity or restoring previously removed claims or assurance.</summary>
    /// <param name="request">The trusted assertion and current candidate produced by the issuer or preceding policy.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs normalization.</param>
    /// <returns>An immutable candidate narrowed from the request candidate, or a typed rejection.</returns>
    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityNormalizationRequest request, CancellationToken cancellationToken = default);
}
