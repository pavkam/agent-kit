// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Maps an assertion from one configured issuer into a normalized identity candidate.</summary>
public interface IIdentityIssuer
{
    /// <summary>Gets the immutable issuer descriptor used for catalog selection and version capture.</summary>
    public IdentityIssuerDescriptor Descriptor { get; }

    /// <summary>Validates expiry and revocation of safe evidence through this trusted issuer.</summary>
    /// <param name="evidence">The safe evidence issued by this issuer; no raw credential is accepted.</param>
    /// <param name="evaluatedAt">The explicit host-clock instant at which validity is evaluated.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs validation.</param>
    /// <returns>A success marker or typed expiry, revocation, issuer, or availability rejection.</returns>
    public ValueTask<IdentityValidationResult> ValidateEvidenceAsync(AuthenticationEvidence evidence, DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default);

    /// <summary>Maps the trusted assertion's external subject and claims without reading raw credentials.</summary>
    /// <param name="assertion">The trusted assertion whose issuer must match <see cref="Descriptor"/>.</param>
    /// <param name="cancellationToken">Signals that the caller no longer needs mapping.</param>
    /// <returns>A normalized candidate or typed rejection.</returns>
    public ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default);
}
