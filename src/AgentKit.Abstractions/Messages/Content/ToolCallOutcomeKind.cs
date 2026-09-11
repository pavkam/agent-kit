// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The portable terminal disposition of one tool call.
/// </summary>
/// <remarks>
/// The kind is a coarse projection of <see cref="ToolCallOutcome.SourceStatus"/>.
/// It never proves whether an effect occurred: failures and cancellation can
/// retain uncertain or partial effects.
/// Use <see cref="ToolCallOutcome.SideEffectCertainty"/> for that evidence and
/// the authoritative terminal record for retry and recovery decisions.
/// </remarks>
public enum ToolCallOutcomeKind
{
    /// <summary>The call executed and produced a successful result.</summary>
    Success,

    /// <summary>Invocation or result processing failed, or the source status is not understood.</summary>
    Failed,

    /// <summary>The requested operation was unknown, invalid, unsupported, or rejected by security or approval policy.</summary>
    Rejected,

    /// <summary>Cancellation or interruption ended the call; effects may already have occurred.</summary>
    Cancelled
}
