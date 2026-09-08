// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes one continuation policy with deterministic canonical snapshots.</summary>
public interface IRunContinuationPolicyConformanceFixture
{
    /// <summary>Gets the policy under test.</summary>
    public IRunContinuationPolicy Policy { get; }

    /// <summary>Creates a correlated continuation snapshot.</summary>
    /// <param name="boundary">The safe boundary.</param>
    /// <param name="causes">Pending causes in capture order.</param>
    /// <param name="requiredStop">The committed primary stop, or null.</param>
    /// <param name="state">The captured total state.</param>
    /// <returns>A valid immutable context for the supplied evidence.</returns>
    public RunContinuationContext CreateContext(
        RunContinuationBoundary boundary,
        ImmutableArray<RunContinuationCause> causes,
        AgentRunOutcome? requiredStop = null,
        AgentRunState state = AgentRunState.Driving);

    /// <summary>Creates a correlated complete assistant boundary.</summary>
    /// <param name="decision">The optional output decision.</param>
    /// <param name="requiresOutput">Whether output acceptance is required.</param>
    /// <returns>A complete committed-turn boundary.</returns>
    public CommittedTurnContinuationBoundary CreateCommittedBoundary(
        OutputProcessingResult? decision = null,
        bool requiresOutput = false);
}
