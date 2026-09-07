// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares whether selection may move past the first candidate alias when it
/// is missing or incompatible.
/// </summary>
/// <remarks>
/// Fallback is a deliberate policy rather than a convenience. Quietly moving
/// to a different provider changes cost, retention, capability, and sometimes
/// jurisdiction, so an application that has not asked for it gets a typed
/// failure instead.
/// </remarks>
public enum ModelFallbackPolicy
{
    /// <summary>
    /// Only the first candidate may be selected. If it is absent or
    /// incompatible, selection fails with a typed result.
    /// </summary>
    FirstCandidateOnly,

    /// <summary>
    /// Candidates are tried in declared order and the first compatible one is
    /// selected. Skipped candidates are reported as diagnostics.
    /// </summary>
    OrderedCandidates,
}
