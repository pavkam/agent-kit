// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Distinguishes complete, incomplete, suspended, and interrupted message
/// output, so a consumer can tell the difference between "this is the final
/// answer" and "something went wrong before this finished" without parsing
/// content or guessing from context.
/// </summary>
/// <remarks>
/// Only <see cref="Complete"/> messages are normally sent to a provider as
/// history; the configured history repair policy decides how a message in
/// any other state is represented (or omitted) in a provider-facing request
/// view, since providers generally have no concept of "half of an answer"
/// as valid conversational input.
/// </remarks>
public enum MessageState
{
    /// <summary>
    /// The message finished normally: its content, and for an assistant
    /// response its usage and stop reason, all validated successfully.
    /// </summary>
    Complete,

    /// <summary>
    /// The message is still being produced — for example, a streaming
    /// candidate that has not yet reached its terminal event and therefore
    /// cannot yet be committed as <see cref="Complete"/>.
    /// </summary>
    Incomplete,

    /// <summary>
    /// The message represents work paused for a deferred operation, such as
    /// a pending human approval or an external execution the run is
    /// waiting on, and is expected to resume rather than remain unfinished
    /// forever.
    /// </summary>
    Suspended,

    /// <summary>
    /// The message was interrupted before completion — by cancellation, a
    /// failed stream, or a provider error mid-response — and its content,
    /// if any, is a partial artifact that must never be presented or
    /// mistaken for a successful completion.
    /// </summary>
    Interrupted
}
