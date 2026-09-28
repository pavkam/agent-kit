// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns the engine's run-admission boundary for the durability coordinator.</summary>
/// <remarks>
/// A distinct type per boundary keeps additive registration idempotent: repeating the engine's registration adds no
/// second handler for the same operation name, which the coordinator rejects outright. Admission is performed by the
/// engine runtime that accepted the run and published its live continuation; a recovering process holds none and
/// refuses rather than admitting a run it never prepared.
/// </remarks>
public sealed class RunAdmissionDurableOperationHandler
    : DurableBoundaryHandler
{
    /// <summary>Initializes the run-admission handler over the engine-wide continuation registry.</summary>
    /// <param name="registry">The non-null registry the admitting runtime publishes its live attempt into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    public RunAdmissionDurableOperationHandler(DurableBoundaryRegistry registry)
        : base(EngineDurableOperations.RunAdmission, registry)
    {
    }
}
