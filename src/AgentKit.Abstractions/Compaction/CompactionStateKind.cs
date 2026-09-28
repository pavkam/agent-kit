// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Categories of durable state referenced alongside a compaction summary.</summary>
public enum CompactionStateKind
{
    /// <summary>An unresolved tool call awaiting a result.</summary>
    PendingToolCall,

    /// <summary>A tool effect that must remain addressable after compaction.</summary>
    ToolEffect,

    /// <summary>An approval decision bound to covered history.</summary>
    Approval,

    /// <summary>Deferred work that must survive compaction.</summary>
    DeferredOperation,

    /// <summary>Goal state referenced by covered entries.</summary>
    Goal,

    /// <summary>Resource mutation evidence from covered entries.</summary>
    Resource,

    /// <summary>Instruction epoch evidence carried into the checkpoint.</summary>
    InstructionEpoch,
}
