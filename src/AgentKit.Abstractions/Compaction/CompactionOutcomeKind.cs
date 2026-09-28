// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Normalized compaction attempt outcomes for observation.</summary>
public enum CompactionOutcomeKind
{
    /// <summary>The attempt succeeded and activated a record.</summary>
    Succeeded,

    /// <summary>The attempt conflicted with concurrent session mutation.</summary>
    Conflict,

    /// <summary>The attempt was cancelled.</summary>
    Cancelled,

    /// <summary>The attempt was rejected by policy.</summary>
    Rejected,

    /// <summary>The attempt did not achieve required reduction.</summary>
    NotReducing,

    /// <summary>The attempt failed unexpectedly.</summary>
    Failed,
}
