// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares whether semantic selection may move past the first candidate alias.
/// </summary>
public enum SemanticFallbackPolicy
{
    /// <summary>Only the first candidate may be selected.</summary>
    FirstCandidateOnly,

    /// <summary>Candidates are tried in declared order until one is compatible.</summary>
    OrderedCandidates,
}
