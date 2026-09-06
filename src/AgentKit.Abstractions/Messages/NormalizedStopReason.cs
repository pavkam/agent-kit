// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable, provider-neutral reason a model response stopped.
/// Provider-specific raw values remain available alongside this normalized
/// value on <see cref="AssistantResponseMetadata.RawStopReason"/>.
/// </summary>
/// <remarks>
/// Providers describe why generation stopped using their own vocabulary
/// ("stop", "length", "tool_calls", "content_filter", and many others,
/// worded differently per vendor). Normalizing that vocabulary into this
/// closed set is what lets the loop, budgets, and output validation make
/// the same decisions regardless of which provider produced the response —
/// for example, treating <see cref="ToolUse"/> as "expect another turn" —
/// without hard-coding provider-specific string comparisons throughout the
/// runtime. The original, unnormalized text is never discarded; it is
/// preserved as diagnostic context.
/// </remarks>
public enum NormalizedStopReason
{
    /// <summary>The response has not yet reached a terminal state.</summary>
    Pending,

    /// <summary>The response completed normally with no further action expected.</summary>
    Completed,

    /// <summary>
    /// The response stopped because a length or token limit was reached
    /// before the model finished producing output.
    /// </summary>
    Length,

    /// <summary>
    /// The response stopped to request one or more tool calls; the loop
    /// should expect another turn after those calls are resolved.
    /// </summary>
    ToolUse,

    /// <summary>
    /// The response terminated because of a provider-side error rather than
    /// a normal stopping condition.
    /// </summary>
    Error,

    /// <summary>
    /// The response was cancelled before completion, typically because the
    /// caller's cancellation token was triggered.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The response was deferred pending an external operation, such as an
    /// approval or a long-running provider-side task, and is expected to
    /// resume later.
    /// </summary>
    Deferred
}
