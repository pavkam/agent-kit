// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes an isolated <see cref="IIdentityIssuer"/> under deterministic conditions.</summary>
public interface IIdentityIssuerConformanceFixture: IConformanceFixture<IIdentityIssuer>
{
    /// <summary>Gets the deterministic evaluation instant used by the composition.</summary>
    /// <value>The fixed clock instant for expiry and skew cases.</value>
    public DateTimeOffset Now { get; }

    /// <summary>Waits until blocking issuer validation begins.</summary>
    /// <param name="cancellationToken">Cancels waiting for the fixture's deterministic entry signal.</param>
    /// <returns>An operation that completes after the issuer has entered validation.</returns>
    public ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates evidence through the implementation's public composition path.</summary>
    /// <param name="scenario">The issuer condition to expose.</param>
    /// <param name="evidence">The safe evidence to validate.</param>
    /// <param name="evaluatedAt">The explicit evaluation instant.</param>
    /// <param name="cancellationToken">Cancels validation without producing a typed rejection.</param>
    /// <returns>The public validation outcome.</returns>
    public ValueTask<IdentityValidationResult> ValidateEvidenceAsync(
        IdentityIssuerConformanceScenario scenario,
        AuthenticationEvidence evidence,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);
}
