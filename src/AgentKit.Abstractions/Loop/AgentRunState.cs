// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the total durable lifecycle state of one accepted run operation.</summary>
/// <remarks>
/// The value is a state-machine observation, not a settlement outcome or a
/// permission to perform the work named by the state. <see cref="Settled"/> is
/// terminal; retry and deferred states remain open durable operations awaiting
/// a later drive. The session coordinator validates every transition.
/// </remarks>
public enum AgentRunState
{
    /// <summary>The durable operation was accepted and may be awaiting process-local drive ownership.</summary>
    Accepted,
    /// <summary>A driver owns the current pass and is coordinating the open operation.</summary>
    Driving,
    /// <summary>The I/O owner is atomically promoting eligible admitted input at a safe boundary.</summary>
    PromotingInput,
    /// <summary>The loop is preparing the next turn through its selected collaborators.</summary>
    PreparingTurn,
    /// <summary>A durably recorded model request is awaiting its provider operation.</summary>
    AwaitingModel,
    /// <summary>The provider response is streaming provisionally and is not yet a committed assistant response.</summary>
    StreamingModel,
    /// <summary>Accepted tool calls are being recorded durably before invocation can begin.</summary>
    RecordingToolCalls,
    /// <summary>Recorded tool calls are awaiting terminal outcomes under the tool scheduler's control.</summary>
    AwaitingTools,
    /// <summary>Terminal tool evidence is being committed and projected in deterministic source order.</summary>
    CommittingToolResults,
    /// <summary>The open durable operation awaits an explicit retry wake condition without settling.</summary>
    WaitingRetry,
    /// <summary>The open durable operation awaits resolution of deferred work without settling.</summary>
    SuspendedDeferred,
    /// <summary>The loop is recording the semantic run outcome before required settlement work.</summary>
    Completing,
    /// <summary>The operation is processing a committed cancellation and stopping new effects.</summary>
    Cancelling,
    /// <summary>The operation is recording a terminal failure before settlement.</summary>
    Failing,
    /// <summary>Required terminal persistence, recovery, hook, and observer work is settling.</summary>
    Settling,
    /// <summary>The operation is terminal, has emitted its settlement event, and can never be reopened.</summary>
    Settled,
}
