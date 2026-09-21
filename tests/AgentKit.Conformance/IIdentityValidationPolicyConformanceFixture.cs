// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes an isolated <see cref="IIdentityValidationPolicy"/> under deterministic options.</summary>
public interface IIdentityValidationPolicyConformanceFixture: IConformanceFixture<IIdentityValidationPolicy>
{
    /// <summary>Gets the deterministic evaluation instant used by the composition.</summary>
    /// <value>The fixed clock instant for expiry and skew cases.</value>
    public DateTimeOffset Now { get; }

    /// <summary>Gets the configured maximum clock skew.</summary>
    public TimeSpan MaximumClockSkew { get; }

    /// <summary>Gets the configured maximum evidence lifetime.</summary>
    public TimeSpan MaximumEvidenceLifetime { get; }

    /// <summary>Waits until blocking validation begins.</summary>
    /// <param name="cancellationToken">Cancels waiting for the fixture's deterministic entry signal.</param>
    /// <returns>An operation that completes after the policy has entered validation.</returns>
    public ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates one candidate through the implementation's public composition path.</summary>
    /// <param name="scenario">The timing or policy condition to expose.</param>
    /// <param name="identity">The candidate to validate.</param>
    /// <param name="cancellationToken">Cancels validation without producing a typed rejection.</param>
    /// <returns>The public validation outcome.</returns>
    public ValueTask<IdentityValidationResult> ValidateAsync(
        IdentityValidationPolicyConformanceScenario scenario,
        ExecutionIdentity identity,
        CancellationToken cancellationToken = default);
}
