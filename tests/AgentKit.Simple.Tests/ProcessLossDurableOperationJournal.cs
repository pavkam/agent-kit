// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

/// <summary>
/// A journal decorator that records what every durable boundary wrote and can simulate losing the process just
/// before one boundary's terminal record is committed.
/// </summary>
/// <remarks>
/// Losing the process is modeled as the terminal write never happening and the attempt aborting: the start and every
/// checkpoint written before that point stay in the inner journal, and nothing after it does, which is exactly the
/// evidence a recovering worker finds after a real crash. The decorator delegates every other call unchanged, so the
/// inner journal's grant consumption, audit, and fencing behavior remain the ones under test.
/// </remarks>
/// <param name="inner">The journal whose behavior this decorator observes and, on demand, interrupts.</param>
internal sealed class ProcessLossDurableOperationJournal(IDurableOperationJournal inner): IDurableOperationJournal
{
    private readonly Lock _gate = new();
    private readonly List<RecoverableOperationDescriptor> _descriptors = [];
    private readonly Dictionary<OperationId, DurableOperationName> _names = [];
    private readonly List<(DurableOperationName Name, DurableCheckpointKind Kind)> _checkpoints = [];
    private readonly List<DurableOperationName> _terminals = [];

    /// <summary>Gets or sets the boundary whose terminal commit is lost, or <see langword="null"/> to lose none.</summary>
    /// <value>The operation name whose next terminal write throws <see cref="SimulatedProcessLossException"/>.</value>
    public DurableOperationName? LoseTerminalOf { get; set; }

    /// <summary>Gets every declaration a start record was written for, in call order.</summary>
    /// <value>A snapshot of the journaled declarations.</value>
    public IReadOnlyList<RecoverableOperationDescriptor> Descriptors
    {
        get
        {
            lock (_gate)
            {
                return [.. _descriptors];
            }
        }
    }

    /// <summary>Gets every checkpoint written, keyed by the boundary that wrote it.</summary>
    /// <value>A snapshot of the boundary names and checkpoint kinds in call order.</value>
    public IReadOnlyList<(DurableOperationName Name, DurableCheckpointKind Kind)> Checkpoints
    {
        get
        {
            lock (_gate)
            {
                return [.. _checkpoints];
            }
        }
    }

    /// <summary>Gets the boundaries whose terminal record was committed, in commit order.</summary>
    /// <value>A snapshot of the operation names that reached a terminal record.</value>
    public IReadOnlyList<DurableOperationName> Terminals
    {
        get
        {
            lock (_gate)
            {
                return [.. _terminals];
            }
        }
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience => inner.SecurityAudience;

    /// <inheritdoc/>
    public async ValueTask<DurableRecordResult> RecordStartAsync(
        AuthorizedDurableRequest<DurableOperationStart> start,
        CancellationToken cancellationToken = default)
    {
        var result = await inner.RecordStartAsync(start, cancellationToken).ConfigureAwait(false);
        if (result is DurableRecorded)
        {
            lock (_gate)
            {
                _descriptors.Add(start.Request.Descriptor);
                _names[start.Request.Descriptor.Address.OperationId] = start.Request.Descriptor.Name;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<DurableRecordResult> RecordCheckpointAsync(
        AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
        CancellationToken cancellationToken = default)
    {
        var result = await inner.RecordCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);
        if (result is DurableRecorded)
        {
            lock (_gate)
            {
                _checkpoints.Add((NameOf(checkpoint.Request.Binding), checkpoint.Request.Kind));
            }
        }

        return result;
    }

    /// <inheritdoc/>
    /// <exception cref="SimulatedProcessLossException">
    /// <see cref="LoseTerminalOf"/> names the boundary this terminal record belongs to.
    /// </exception>
    public async ValueTask<DurableRecordResult> RecordTerminalAsync(
        AuthorizedDurableRequest<DurableOperationResult> result,
        CancellationToken cancellationToken = default)
    {
        var name = NameOf(result.Request.Binding);
        if (LoseTerminalOf is { } lost && lost == name)
        {
            throw new SimulatedProcessLossException();
        }

        var recorded = await inner.RecordTerminalAsync(result, cancellationToken).ConfigureAwait(false);
        if (recorded is DurableRecorded)
        {
            lock (_gate)
            {
                _terminals.Add(name);
            }
        }

        return recorded;
    }

    /// <inheritdoc/>
    public ValueTask<DurableRecordResult> RecordWaitingAsync(
        AuthorizedDurableRequest<DurableOperationWaiting> waiting,
        CancellationToken cancellationToken = default) =>
        inner.RecordWaitingAsync(waiting, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
        AuthorizedDurableRequest<DurableOperationAddress> address,
        CancellationToken cancellationToken = default) =>
        inner.LoadEvidenceAsync(address, cancellationToken);

    private DurableOperationName NameOf(DurableOperationBinding binding)
    {
        lock (_gate)
        {
            return _names.TryGetValue(binding.Address.OperationId, out var name) ? name : default;
        }
    }
}
