// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Decides how one blocked run-event delivery attempt is handled.</summary>
/// <remarks>
/// A <see cref="RunEventDelivery.Required"/> sink may only ever receive <see cref="Wait"/>: <see cref="Drop"/>
/// would silently lose durable evidence and <see cref="Disconnect"/> would silently stop a required delivery,
/// neither of which fails the run closed the way a required sink's contract demands. Only a
/// <see cref="RunEventDelivery.BestEffort"/> sink or a live <c>ReadAllAsync</c> subscriber may receive
/// <see cref="Drop"/> or <see cref="Disconnect"/>.
/// </remarks>
public enum BackpressureDecision
{
    /// <summary>Retry the delivery attempt after the caller's own backoff.</summary>
    Wait,

    /// <summary>Skip this event for the blocked destination and continue; the run itself is unaffected.</summary>
    Drop,

    /// <summary>Close the blocked destination's subscription; the run itself is unaffected.</summary>
    Disconnect,
}
