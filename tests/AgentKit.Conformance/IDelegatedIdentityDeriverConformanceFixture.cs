// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes an isolated <see cref="IDelegatedIdentityDeriver"/> under deterministic conditions.</summary>
public interface IDelegatedIdentityDeriverConformanceFixture: IConformanceFixture<IDelegatedIdentityDeriver>
{
    /// <summary>Gets the deterministic evaluation instant used by the composition.</summary>
    /// <value>The fixed clock instant for expiry cases.</value>
    public DateTimeOffset Now { get; }

    /// <summary>Gets the configured maximum delegation depth.</summary>
    public int MaximumDelegationDepth { get; }

    /// <summary>Waits until blocking derivation begins.</summary>
    /// <param name="cancellationToken">Cancels waiting for the fixture's deterministic entry signal.</param>
    /// <returns>An operation that completes after the deriver has entered derivation.</returns>
    public ValueTask WaitUntilBlockedAsync(CancellationToken cancellationToken = default);

    /// <summary>Derives a child identity through the implementation's public composition path.</summary>
    /// <param name="scenario">The delegation condition to expose.</param>
    /// <param name="request">The bounded delegation request.</param>
    /// <param name="cancellationToken">Cancels derivation without producing a typed rejection.</param>
    /// <returns>The public resolved or rejected identity outcome.</returns>
    public ValueTask<IdentityResolutionResult> DeriveAsync(
        DelegatedIdentityDeriverConformanceScenario scenario,
        DelegatedIdentityRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates one valid parent identity for narrowing cases.</summary>
    /// <returns>A parent with two claims and strong assurance.</returns>
    public ExecutionIdentity CreateParentIdentity();
}
