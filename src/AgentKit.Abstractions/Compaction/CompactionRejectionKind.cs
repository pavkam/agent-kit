// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The category of a deliberate, policy-driven decision not to compact,
/// as opposed to an unexpected failure.
/// </summary>
public enum CompactionRejectionKind
{
    /// <summary>Compaction is disabled for this session, branch, or agent.</summary>
    Disabled,

    /// <summary>No cut could be found that respects structural and policy constraints.</summary>
    NoSafeCut,

    /// <summary>The eligible source range exceeds a configured processing limit.</summary>
    SourceLimitExceeded,

    /// <summary>A configured policy constraint was violated by every candidate cut.</summary>
    PolicyViolation,

    /// <summary>
    /// The request's <see cref="CompactionRequest.Deadline"/> had already passed when the attempt started, so the
    /// compactor declined before any session read or append.
    /// </summary>
    DeadlineExceeded
}
