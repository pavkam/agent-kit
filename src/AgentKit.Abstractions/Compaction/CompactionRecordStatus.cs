// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The activation status of a durably recorded compaction.</summary>
public enum CompactionRecordStatus
{
    /// <summary>The compaction was successfully activated and its checkpoint is authoritative.</summary>
    Active,

    /// <summary>The compaction attempt was rejected and never activated.</summary>
    Rejected
}
