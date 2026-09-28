// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Counts invocations of one operation name and returns a scripted terminal result.</summary>
/// <remarks>
/// The invocation count is what proves the recovery rules: a crash after a recorded outcome must produce zero
/// additional invocations, while a proven-absent start must produce exactly one.
/// </remarks>
internal sealed class RecordingDurableOperationHandler: IDurableOperationHandler
{
    private readonly Func<DurableInvocationContext, DurableOperationResult> _result;
    private int _invocations;

    /// <summary>Initializes a handler for one operation name.</summary>
    /// <param name="operationName">The name this handler claims, or null for the shared test operation name.</param>
    /// <param name="result">The result factory, or null to produce a completed, definitely-performed result.</param>
    internal RecordingDurableOperationHandler(
        DurableOperationName? operationName = null,
        Func<DurableInvocationContext, DurableOperationResult>? result = null)
    {
        OperationName = operationName ?? new DurableOperationName("tool.call");
        _result = result ?? DefaultResult;
    }

    /// <inheritdoc/>
    public DurableOperationName OperationName { get; }

    /// <summary>Gets how many times the effect was invoked.</summary>
    /// <value>The completed and in-flight invocation count.</value>
    internal int Invocations => Volatile.Read(ref _invocations);

    /// <summary>Gets or sets an exception the handler throws instead of producing a result.</summary>
    /// <value>Null to return normally.</value>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a callback run inside the invocation, before any result is produced.</summary>
    /// <value>Null to perform no additional work.</value>
    internal Func<ValueTask>? DuringInvocation { get; set; }

    /// <inheritdoc/>
    public async ValueTask<DurableOperationResult> InvokeAsync(
        DurableInvocationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _invocations);
        if (DuringInvocation is { } callback)
        {
            await callback().ConfigureAwait(false);
        }

        return Failure is { } failure ? throw failure : _result(context);
    }

    private static DurableOperationResult DefaultResult(DurableInvocationContext context) =>
        new(
            context.Binding,
            DurableOperationState.Completed,
            SideEffectCertainty.DefinitelyPerformed,
            DurableJournalTestData.Payload(9),
            context.Lease.FencingToken,
            DurableJournalTestData.Now);
}
