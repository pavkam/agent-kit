// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates isolated security-decision-store composition for one conformance case.</summary>
public interface ISecurityDecisionStoreConformanceFixture: IConformanceFixture<ISecurityDecisionStore>
{
    /// <summary>Reads every decision recorded by the store under test in arrival order.</summary>
    /// <param name="store">The composed store instance.</param>
    /// <returns>The recorded decisions visible through the implementation's supported inspection surface.</returns>
    public IReadOnlyList<SecurityDecision> ReadRecorded(ISecurityDecisionStore store);

    /// <summary>Disposes the current subject and composes a fresh one against the same persistence target.</summary>
    /// <param name="cancellationToken">Cancels before the replacement subject is returned.</param>
    /// <returns>The newly composed store.</returns>
    public ValueTask<ISecurityDecisionStore> RecreateAsync(CancellationToken cancellationToken = default);
}
