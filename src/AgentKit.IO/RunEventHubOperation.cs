// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Defines the bounded operation dimensions emitted by run-event fan-out.</summary>
internal enum RunEventHubOperation
{
    /// <summary>Registering a new non-owning recipient.</summary>
    Subscribe,
    /// <summary>Explicitly releasing one non-owning recipient.</summary>
    Unsubscribe,
    /// <summary>Offering one presequenced event to its captured recipients.</summary>
    Publish,
    /// <summary>Disconnecting a saturated recipient without blocking the producer.</summary>
    Disconnect,
    /// <summary>Reading one subscriber until end, abandonment or cancellation.</summary>
    Read,
    /// <summary>Sealing normal producer event delivery.</summary>
    Complete,
    /// <summary>Releasing active hub-owned subscriptions.</summary>
    Dispose,
}
