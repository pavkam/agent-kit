// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the atomic outcome of storing a terminal approval response.</summary>
public enum ApprovalStoreResolveResult
{
    /// <summary>The response terminally resolved the pending request.</summary>
    Resolved,
    /// <summary>The same response was already recorded.</summary>
    AlreadyResolved,
    /// <summary>No matching pending request exists.</summary>
    NotFound,
    /// <summary>The request is already bound to different terminal evidence.</summary>
    Conflict,
}
