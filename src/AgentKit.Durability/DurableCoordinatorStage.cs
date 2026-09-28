// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Enumerates the bounded coordinator stages reported as a metric dimension.</summary>
/// <remarks>Stages name package-defined phases only; they never carry operation names, payloads, or identities.</remarks>
internal enum DurableCoordinatorStage
{
    /// <summary>One complete coordinated durable execution.</summary>
    Execute,

    /// <summary>One complete coordinated recovery attempt.</summary>
    Recover,

    /// <summary>Durability runtime activation for a captured context.</summary>
    Activate,

    /// <summary>Execution-lease acquisition for an operation address.</summary>
    AcquireLease,

    /// <summary>Coordinator-owned execution-lease renewal.</summary>
    RenewLease,

    /// <summary>The durable start record that precedes any effect.</summary>
    RecordStart,

    /// <summary>Handoff to an external durable backend.</summary>
    Dispatch,

    /// <summary>An intermediate durable checkpoint.</summary>
    Checkpoint,

    /// <summary>A durable record that the operation is waiting on an owner, approval, or not-before instant.</summary>
    RecordWaiting,

    /// <summary>The terminal durable record.</summary>
    RecordTerminal,

    /// <summary>Recovery evidence loading.</summary>
    LoadEvidence,

    /// <summary>The recovery-policy decision over loaded evidence.</summary>
    Decide,

    /// <summary>Backend reconciliation of true external state.</summary>
    Reconcile,
}
