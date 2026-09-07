// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes an isolated identity normalizer under deterministic issuer and policy conditions.</summary>
public interface IIdentityNormalizerConformanceFixture
{
    /// <summary>Gets the deterministic evaluation instant used by the composition.</summary>
    /// <value>The fixed clock instant for expiry cases.</value>
    public DateTimeOffset Now { get; }
    /// <summary>Gets the expected issuer descriptor version.</summary>
    /// <value>The version that successful resolution must capture from the configured issuer descriptor.</value>
    public IdentityVersion ExpectedIssuerVersion { get; }

    /// <summary>Waits until blocking normalization begins.</summary>
    /// <param name="cancellationToken">Cancels waiting for the fixture's deterministic entry signal.</param>
    /// <returns>An operation that completes after the blocking issuer has entered normalization.</returns>
    public ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves one assertion through the implementation's public composition path.</summary>
    /// <param name="assertion">The trusted bounded assertion to normalize.</param>
    /// <param name="scenario">The issuer or policy condition to expose.</param>
    /// <param name="cancellationToken">Cancels normalization without producing a typed rejection.</param>
    /// <returns>The public resolved or rejected identity outcome.</returns>
    public ValueTask<IdentityResolutionResult> ResolveAsync(
        IdentityAssertion assertion,
        IdentityNormalizerScenario scenario,
        CancellationToken cancellationToken = default);
}
