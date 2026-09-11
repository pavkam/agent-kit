// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Identifies whether a presequenced event was accepted by the run-local fan-out boundary.</summary>
internal enum RunEventPublicationOutcome
{
    /// <summary>The recipient set was captured and each recipient accepted the event or was explicitly disconnected.</summary>
    Published,
    /// <summary>The hub has ended; no recipient or sequence changed.</summary>
    HubClosed,
    /// <summary>The sequence did not advance strictly beyond the last accepted event; no recipient changed.</summary>
    OutOfOrder,
}
