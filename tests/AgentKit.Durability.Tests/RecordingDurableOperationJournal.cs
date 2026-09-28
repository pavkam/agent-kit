// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Records every authorized journal call and answers with whatever the test scripted.</summary>
/// <remarks>
/// The double performs no authorization and no fencing of its own. Grant consumption and audit are the concrete
/// journal adapter's contract and are covered by that adapter's own suite; what matters here is which calls the
/// component under test makes, in what order, and with which presented evidence.
/// </remarks>
internal sealed class RecordingDurableOperationJournal: IDurableOperationJournal
{
    private readonly List<string> _calls = [];

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("test.durable.journal");

    /// <summary>Gets the journal method names in call order.</summary>
    /// <value>A live list of method names, such as <c>RecordStart</c>.</value>
    internal IReadOnlyList<string> Calls => _calls;

    /// <summary>Gets the start requests this journal received, in order.</summary>
    /// <value>A live list of the exact authorized requests presented.</value>
    internal List<AuthorizedDurableRequest<DurableOperationStart>> Starts { get; } = [];

    /// <summary>Gets the terminal requests this journal received, in order.</summary>
    /// <value>A live list of the exact authorized requests presented.</value>
    internal List<AuthorizedDurableRequest<DurableOperationResult>> Terminals { get; } = [];

    /// <summary>Gets the waiting requests this journal received, in order.</summary>
    /// <value>A live list of the exact authorized requests presented.</value>
    internal List<AuthorizedDurableRequest<DurableOperationWaiting>> Waits { get; } = [];

    /// <summary>Gets the checkpoint requests this journal received, in order.</summary>
    /// <value>A live list of the exact authorized requests presented.</value>
    internal List<AuthorizedDurableRequest<DurableCheckpoint>> Checkpoints { get; } = [];

    /// <summary>Gets or sets the result every write reports.</summary>
    /// <value>Null to report a fresh <see cref="DurableRecorded"/> at the presented generation.</value>
    internal DurableRecordResult? WriteResult { get; set; }

    /// <summary>Gets or sets the evidence result <see cref="LoadEvidenceAsync"/> reports.</summary>
    /// <value>Null to report <see cref="RecoveryEvidenceNotFound"/>.</value>
    internal RecoveryEvidenceResult? EvidenceResult { get; set; }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordStartAsync(
        AuthorizedDurableRequest<DurableOperationStart> start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("RecordStart");
        Starts.Add(start);
        return ValueTask.FromResult(Write(start.Request.FencingToken));
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordCheckpointAsync(
        AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("RecordCheckpoint");
        Checkpoints.Add(checkpoint);
        return ValueTask.FromResult(Write(checkpoint.Request.FencingToken));
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordTerminalAsync(
        AuthorizedDurableRequest<DurableOperationResult> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("RecordTerminal");
        Terminals.Add(result);
        return ValueTask.FromResult(Write(result.Request.FencingToken));
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        AuthorizedDurableRequest<DurableOperationWaiting> waiting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(waiting);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("RecordWaiting");
        Waits.Add(waiting);
        return ValueTask.FromResult(Write(waiting.Request.FencingToken));
    }

    /// <inheritdoc/>
    public ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        AuthorizedDurableRequest<DurableOperationAddress> address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("LoadEvidence");
        return ValueTask.FromResult(
            EvidenceResult ?? new RecoveryEvidenceNotFound(address.Request));
    }

    private DurableRecordResult Write(FencingToken presented) =>
        WriteResult ?? new DurableRecorded(presented, DurableJournalTestData.Now);
}
