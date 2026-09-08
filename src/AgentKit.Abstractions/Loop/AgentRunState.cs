// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the total lifecycle state of one accepted run operation.</summary>
public enum AgentRunState
{
    /// <summary>The durable operation is accepted and may be awaiting a driver.</summary>
    Accepted,
    /// <summary>A driver currently coordinates the operation.</summary>
    Driving,
    /// <summary>Eligible admitted input is being promoted.</summary>
    PromotingInput,
    /// <summary>The next model turn is being prepared.</summary>
    PreparingTurn,
    /// <summary>A recorded model request is awaiting its provider operation.</summary>
    AwaitingModel,
    /// <summary>The provider response is streaming provisionally.</summary>
    StreamingModel,
    /// <summary>Accepted tool calls are being durably recorded.</summary>
    RecordingToolCalls,
    /// <summary>Recorded tool calls are awaiting terminal outcomes.</summary>
    AwaitingTools,
    /// <summary>Terminal tool evidence is being materialized in source order.</summary>
    CommittingToolResults,
    /// <summary>The open operation waits for a retry wake condition.</summary>
    WaitingRetry,
    /// <summary>The open operation waits for deferred work to resolve.</summary>
    SuspendedDeferred,
    /// <summary>The semantic run outcome is being completed.</summary>
    Completing,
    /// <summary>The operation is processing a committed cancellation.</summary>
    Cancelling,
    /// <summary>The operation is recording a terminal failure.</summary>
    Failing,
    /// <summary>Required post-run work is settling.</summary>
    Settling,
    /// <summary>The operation is terminal and can never be reopened.</summary>
    Settled,
}
