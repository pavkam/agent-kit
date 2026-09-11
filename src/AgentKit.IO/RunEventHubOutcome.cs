// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Defines the bounded outcome dimensions emitted by run-event fan-out.</summary>
internal enum RunEventHubOutcome
{
    /// <summary>The local operation completed successfully.</summary>
    Succeeded,
    /// <summary>The hub no longer accepts new work.</summary>
    HubClosed,
    /// <summary>The simultaneous subscriber limit was reached.</summary>
    CapacityReached,
    /// <summary>An event did not advance the accepted sequence.</summary>
    OutOfOrder,
    /// <summary>A saturated subscriber was explicitly disconnected.</summary>
    SlowConsumer,
    /// <summary>Premature hub disposal terminated delivery.</summary>
    HubDisposed,
    /// <summary>The caller cancelled this local operation only.</summary>
    Cancelled,
    /// <summary>The reader explicitly abandoned its local subscription.</summary>
    Abandoned,
    /// <summary>An unexpected local operation failure occurred.</summary>
    Faulted,
}
