// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Classifies the terminal state of a bounded content search.</summary>
public enum FileSearchStatus
{
    /// <summary>The complete search retained at least one match.</summary>
    Success,
    /// <summary>The complete search found no matches.</summary>
    NoMatches,
    /// <summary>The authorized base directory did not exist.</summary>
    NotFound,
    /// <summary>Authorization or a no-follow boundary denied observation.</summary>
    Denied,
    /// <summary>A file, byte, depth, or result bound stopped the search.</summary>
    LimitExceeded,
    /// <summary>The configured duration elapsed before traversal completed.</summary>
    TimedOut,
    /// <summary>The host could not complete the search for a non-sensitive reason.</summary>
    Failed,
}
