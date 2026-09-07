// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies a terminal glob traversal.</summary>
public enum GlobStatus
{
    /// <summary>At least one complete deterministic match was returned.</summary>
    Success,
    /// <summary>The complete traversal found no matches.</summary>
    NoMatches,
    /// <summary>The authorized base directory did not exist.</summary>
    NotFound,
    /// <summary>Authorization or the sandbox boundary denied traversal.</summary>
    Denied,
    /// <summary>A visit, depth, or retained-result bound stopped traversal.</summary>
    LimitExceeded,
    /// <summary>The host could not complete traversal for a non-sensitive reason.</summary>
    Failed,
}
