// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Provides a coordinator-shaped checkpoint writer for boundary-handler tests.</summary>
public sealed class StubCheckpointWriter(RecoverableOperationDescriptor operation, FencingToken fencingToken): IDurableCheckpointWriter
{
    /// <inheritdoc/>
    public DurableOperationBinding Binding => operation.Binding;

    /// <inheritdoc/>
    public FencingToken FencingToken => fencingToken;

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        DurableCheckpointKind kind,
        OperationPayload state,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<DurableRecordResult>(new DurableRecorded(fencingToken, DateTimeOffset.UnixEpoch));

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        DurableWaitCondition condition,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<DurableRecordResult>(new DurableRecorded(fencingToken, DateTimeOffset.UnixEpoch));
}
