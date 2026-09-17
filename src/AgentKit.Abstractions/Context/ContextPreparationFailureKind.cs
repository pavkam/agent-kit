// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The category of a failure encountered while assembling one provider-ready request.</summary>
public enum ContextPreparationFailureKind
{
    /// <summary>The eligible history contains no messages to send.</summary>
    EmptyHistory,

    /// <summary>
    /// A tool call in the eligible history has no matching terminal result,
    /// a tool result references a call that is not present or precedes it, one
    /// call identity is requested more than once, or one call has more than
    /// one terminal result.
    /// </summary>
    BrokenToolCallCausality,

    /// <summary>An unclassified failure occurred during assembly.</summary>
    Unknown,

    /// <summary>
    /// A message carries a content part its role may not carry: a
    /// <see cref="ToolCallPart"/> outside an <see cref="AssistantMessage"/>, or
    /// a <see cref="ToolResultPart"/> outside a <see cref="ToolMessage"/>.
    /// Because each half of a call is confined to its own role, this also
    /// rejects a call and its result sharing one message.
    /// </summary>
    InvalidRolePartCombination,

    /// <summary>
    /// An instruction message is not <see cref="MessageState.Complete"/>, or is not a
    /// <see cref="SystemMessage"/> or <see cref="DeveloperMessage"/>. Instructions enter a request
    /// exclusively through the explicit instruction set and are never repaired or silently excluded
    /// the way history is, so a violation fails assembly instead.
    /// </summary>
    InvalidInstructionMessage
}
