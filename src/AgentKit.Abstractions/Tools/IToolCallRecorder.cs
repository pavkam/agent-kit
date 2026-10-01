// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Durably records the accepted and terminal facts of one tool call through the run's selected session capability.</summary>
/// <remarks>
/// <para>
/// The executor calls <see cref="RecordAcceptedAsync"/> after resolution, argument validation, planning, and
/// authorization and before any invoker starts; an effect begins only after that call returns
/// <see cref="ToolCallRecorded"/>. It calls <see cref="RecordTerminalAsync"/> once for every identified call, including
/// pre-invocation rejections, before the loop projects the result into history.
/// </para>
/// <para>
/// A recorder owns accepted/terminal state and idempotent or optimistic recording; it is not an event sink and never
/// invokes tools. A retried recording is never an invocation retry. Implementations receive the exact
/// <see cref="SessionExecutionCapability"/> on every write and must not resolve an unkeyed session coordinator or
/// rediscover a store. A recorder must be safe to call concurrently for different calls.
/// </para>
/// </remarks>
public interface IToolCallRecorder
{
    /// <summary>Commits the durable fact that an authorized call is about to be invoked.</summary>
    /// <param name="accepted">The nonnull accepted-call evidence; repeating the same <see cref="AcceptedToolCall.CallId"/> must be idempotent.</param>
    /// <param name="session">The nonnull invocation-only session capability that selects the coordinator and immutable profile.</param>
    /// <param name="target">The nonnull branch and lane that receive the record.</param>
    /// <param name="cancellationToken">Cancels the write; cancellation propagates and is never reported as a rejection.</param>
    /// <returns><see cref="ToolCallRecorded"/> only when the record is durable, otherwise a typed <see cref="ToolCallRecordRejected"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="accepted"/>, <paramref name="session"/>, or <paramref name="target"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ToolCallRecordResult> RecordAcceptedAsync(
        AcceptedToolCall accepted,
        SessionExecutionCapability session,
        ToolCallSessionTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>Commits the authoritative terminal outcome of one call.</summary>
    /// <param name="result">The nonnull terminal record; when it carries acceptance evidence the recorder validates it against the accepted record.</param>
    /// <param name="session">The nonnull invocation-only session capability that selects the coordinator and immutable profile.</param>
    /// <param name="target">The nonnull branch and lane that receive the record.</param>
    /// <param name="cancellationToken">Cancels the write; cancellation propagates and is never reported as a rejection.</param>
    /// <returns><see cref="ToolCallRecorded"/> only when the record is durable, otherwise a typed <see cref="ToolCallRecordRejected"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/>, <paramref name="session"/>, or <paramref name="target"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<ToolCallRecordResult> RecordTerminalAsync(
        ToolCallResult result,
        SessionExecutionCapability session,
        ToolCallSessionTarget target,
        CancellationToken cancellationToken = default);
}
