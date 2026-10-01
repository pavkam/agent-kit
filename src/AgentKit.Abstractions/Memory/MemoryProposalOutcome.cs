// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies how a memory proposal ended.</summary>
public enum MemoryProposalOutcome
{
    /// <summary>Policy allowed the proposal and the store made it durable.</summary>
    Accepted = 0,

    /// <summary>Policy refused the proposal before any write.</summary>
    PolicyDenied = 1,

    /// <summary>The proposal was not written: the profile is unavailable, a grant was refused, or the store rejected it.</summary>
    Rejected = 2,
}
