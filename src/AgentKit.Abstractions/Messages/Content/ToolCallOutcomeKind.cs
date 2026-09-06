// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable terminal disposition of one tool call.
/// </summary>
/// <remarks>
/// Every accepted tool call reaches exactly one of these terminal
/// dispositions, recorded on its <see cref="ToolCallOutcome"/>. Keeping
/// authorization failures (<see cref="Rejected"/>) distinct from execution
/// failures (<see cref="Failed"/>) and from cancellation
/// (<see cref="Cancelled"/>) matters because each implies a different
/// retry, telemetry, and user-facing story: a rejection means the caller
/// was never allowed to try, a failure means it tried and did not succeed,
/// and a cancellation means the run stopped waiting before either was
/// determined.
/// </remarks>
public enum ToolCallOutcomeKind
{
    /// <summary>The call executed and produced a successful result.</summary>
    Success,

    /// <summary>The call was authorized and executed but failed.</summary>
    Failed,

    /// <summary>The call was denied authorization before execution ever began.</summary>
    Rejected,

    /// <summary>The call was cancelled before it produced a result.</summary>
    Cancelled
}
