// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies why an agent-to-agent message was not admitted.</summary>
public enum AgentMessageRejectionKind
{
    /// <summary>The recipient agent is not hosted by the channel's engine.</summary>
    UnknownRecipient = 0,

    /// <summary>The idempotency identity was already used for different content.</summary>
    Conflict = 1,

    /// <summary>The recipient session's input capacity is exhausted; retrying later may succeed.</summary>
    CapacityExceeded = 2,

    /// <summary>Admission refused the message for another safe reason, such as an unavailable session or authorization.</summary>
    Rejected = 3,
}
