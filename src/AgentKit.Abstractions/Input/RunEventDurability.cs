// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// States whether a run event is an in-memory publication or durable session
/// evidence that can be replayed.
/// </summary>
public enum RunEventDurability
{
    /// <summary>
    /// The event is provisional live delivery and is not itself durable
    /// session evidence.
    /// </summary>
    Live,

    /// <summary>
    /// The event corresponds to durable evidence and retains its protocol
    /// sequence during replay.
    /// </summary>
    Durable,
}
