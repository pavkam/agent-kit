// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the state a delegated child reported to its parent.</summary>
public enum DelegationStatus
{
    /// <summary>The child-admission intent is durably committed but the child has not settled.</summary>
    Dispatched = 0,

    /// <summary>The child settled with a successful outcome. The result is still untrusted until validated.</summary>
    Succeeded = 1,

    /// <summary>The child settled without success.</summary>
    Failed = 2,

    /// <summary>The child was cancelled.</summary>
    Cancelled = 3,

    /// <summary>The child cannot progress without external authority or change.</summary>
    Blocked = 4,
}
