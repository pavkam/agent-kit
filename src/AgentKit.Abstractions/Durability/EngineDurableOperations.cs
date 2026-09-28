// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names and versions the recoverable operation engine admission can journal.</summary>
/// <remarks>
/// Admission is journaled after the session has durably accepted the run, not before. The accepted run is already
/// durable session truth; the durable record exists so a recovering worker can discover an accepted run whose driving
/// process was lost between acceptance and the first model attempt, which is otherwise the one window where a run is
/// durably open with no journaled operation at all.
/// </remarks>
public static class EngineDurableOperations
{
    /// <summary>Gets the operation name for admitting one run and handing it to its loop.</summary>
    /// <value>The name a durability profile must enable before run admission is journaled.</value>
    public static DurableOperationName RunAdmission { get; } = new("agentkit.engine.run_admission");

    /// <summary>Gets the published version of the run-admission manifest shape.</summary>
    /// <value>The version recorded on every run-admission declaration.</value>
    public static DurableOperationVersion RunAdmissionVersion { get; } = new("v1");

    /// <summary>Gets every operation name engine admission can journal.</summary>
    /// <value>The complete additive set a profile may enable; admission journals no other name.</value>
    public static ImmutableArray<DurableOperationName> All { get; } = [RunAdmission];
}
