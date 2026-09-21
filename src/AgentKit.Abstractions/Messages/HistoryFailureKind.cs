// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The category of a failure encountered while preparing conversation history for one model request.</summary>
public enum HistoryFailureKind
{
    /// <summary>The eligible history contains no complete messages to send.</summary>
    EmptyHistory,

    /// <summary>
    /// Tool-call causality in the eligible history is broken: missing results, orphan results, duplicate call
    /// identities, or repeated terminal results.
    /// </summary>
    BrokenToolCallCausality,

    /// <summary>
    /// A message carries a content part its role may not carry, or a call and result share one message.
    /// </summary>
    InvalidRolePartCombination,
}
