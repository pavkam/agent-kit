// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// A durable coordinator that records each declaration and runs the live continuation its boundary published,
/// standing in for the real coordinator so a consumer's sandwich can be tested in isolation.
/// </summary>
/// <remarks>
/// <para>
/// The fake performs no journaling and no authorization. It does what the real coordinator does at the boundary a
/// consumer can observe: it receives the declaration, hands the published continuation a
/// <see cref="DurableInvocationContext"/> whose writer records into <see cref="Writers"/>, and returns that
/// continuation's terminal result. A consumer that forgot to publish a continuation fails the test exactly as it
/// would fail a recovering process.
/// </para>
/// <para>
/// Set <see cref="Failure"/> to simulate a coordinator that cannot journal; it is thrown before the continuation
/// runs, so the boundary's effect never happens.
/// </para>
/// </remarks>
/// <param name="registry">The registry the consumer under test publishes its continuations into.</param>
public sealed class RecordingBoundaryCoordinator(DurableBoundaryRegistry registry): IDurableExecutionCoordinator
{
    private readonly Lock _gate = new();
    private readonly List<RecoverableOperationDescriptor> _executions = [];
    private readonly List<RecordingCheckpointWriter> _writers = [];

    /// <summary>Gets or sets the exception thrown instead of running the continuation, or <see langword="null"/> to run it.</summary>
    /// <value>The failure to simulate, or <see langword="null"/>.</value>
    public Exception? Failure { get; set; }

    /// <summary>Gets or sets the refusal every continuation's writer returns, or <see langword="null"/> to accept writes.</summary>
    /// <value>
    /// A typed refusal such as <see cref="DurableRecordFailed"/> that simulates a journal rejecting mid-operation
    /// evidence while the coordinator itself is healthy.
    /// </value>
    public DurableRecordResult? WriteRefusal { get; set; }

    /// <summary>Gets every declaration presented to <see cref="ExecuteAsync"/>, in call order.</summary>
    /// <value>A snapshot of the declarations.</value>
    public IReadOnlyList<RecoverableOperationDescriptor> Executions
    {
        get
        {
            lock (_gate)
            {
                return [.. _executions];
            }
        }
    }

    /// <summary>Gets the writer handed to each continuation, parallel to <see cref="Executions"/>.</summary>
    /// <value>A snapshot of the writers that received the checkpoints and waits.</value>
    public IReadOnlyList<RecordingCheckpointWriter> Writers
    {
        get
        {
            lock (_gate)
            {
                return [.. _writers];
            }
        }
    }

    /// <inheritdoc/>
    public async Task<DurableOperationResult> ExecuteAsync(
        RecoverableOperationDescriptor operation,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _executions.Add(operation);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }

        var continuation = registry.Resolve(operation.Address.OperationId)
            ?? throw new InvalidOperationException("No live boundary continuation was published for the declaration.");
        var lease = new StubExecutionLease(operation.Address, new FencingToken(1));
        var writer = new RecordingCheckpointWriter(operation, lease.FencingToken, WriteRefusal);
        lock (_gate)
        {
            _writers.Add(writer);
        }

        return await continuation(new DurableInvocationContext(operation, lease, writer, hooks), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Recovery is exercised through the real coordinator, never through this fake.</exception>
    public Task<DurableOperationResult> RecoverAsync(
        DurableOperationAddress address,
        DurableExecutionContext context,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Recovery is exercised through the real durable coordinator.");
}
