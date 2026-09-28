// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Projects <see cref="DurableCoordinatorStage"/> values onto stable bounded telemetry text.</summary>
internal static class DurableCoordinatorStageExtensions
{
    extension(DurableCoordinatorStage stage)
    {
        /// <summary>Gets the stable lowercase dimension value for this stage.</summary>
        /// <returns>A bounded telemetry token that never varies with identities or content.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The stage is not a defined enumeration member.</exception>
        internal string ToStableValue() => stage switch
        {
            DurableCoordinatorStage.Execute => "execute",
            DurableCoordinatorStage.Recover => "recover",
            DurableCoordinatorStage.Activate => "activate",
            DurableCoordinatorStage.AcquireLease => "acquire_lease",
            DurableCoordinatorStage.RenewLease => "renew_lease",
            DurableCoordinatorStage.RecordStart => "record_start",
            DurableCoordinatorStage.Dispatch => "dispatch",
            DurableCoordinatorStage.Checkpoint => "checkpoint",
            DurableCoordinatorStage.RecordWaiting => "record_waiting",
            DurableCoordinatorStage.RecordTerminal => "record_terminal",
            DurableCoordinatorStage.LoadEvidence => "load_evidence",
            DurableCoordinatorStage.Decide => "decide",
            DurableCoordinatorStage.Reconcile => "reconcile",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown durable coordinator stage."),
        };
    }
}
