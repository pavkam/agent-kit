// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines how assembly responds when mandatory content exceeds the allocated budget.</summary>
public enum ContextOverflowBehavior
{
    /// <summary>Assembly fails with a typed context-preparation failure before provider I/O.</summary>
    Fail,

    /// <summary>Assembly may invoke configured compaction when available; otherwise it fails.</summary>
    CompactWhenConfigured,
}
