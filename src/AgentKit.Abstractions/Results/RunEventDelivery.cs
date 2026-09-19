// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies whether a registered run-event sink's delivery must complete before settlement or may drop under load.</summary>
public enum RunEventDelivery
{
    /// <summary>The publisher's acceptance boundary does not complete until this sink has durably accepted the event.</summary>
    Required,

    /// <summary>The publisher isolates this sink's failure or backpressure; it never blocks other sinks or the run.</summary>
    BestEffort,
}
