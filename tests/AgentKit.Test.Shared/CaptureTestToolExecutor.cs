// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

/// <summary>Detects accidental tool execution while testing run-plan compilation and capture paths.</summary>
/// <remarks>Run services must retain this instance without executing tool batches through it.</remarks>
public sealed class CaptureTestToolExecutor: IToolExecutor
{
    private int _executions;

    /// <summary>Gets the count of accidental execution attempts.</summary>
    /// <value>A thread-safe count, normally zero in capture tests.</value>
    public int Executions => Volatile.Read(ref _executions);

    /// <summary>Fails if a run attempts to execute tools through this placeholder.</summary>
    /// <param name="capture">The catalog capture supplied for the batch.</param>
    /// <param name="calls">The requested calls whose unexpected arrival is counted.</param>
    /// <param name="capability">The execution capability bound to the run.</param>
    /// <param name="cancellationToken">Unused because this fake must never execute.</param>
    /// <returns>No result; valid calls always throw.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">A tool batch was attempted.</exception>
    public Task<ToolBatchResult> ExecuteAsync(
        IToolCatalogCapture capture,
        ImmutableArray<ToolCallRequest> calls,
        ToolExecutionCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(capability);
        _ = Interlocked.Increment(ref _executions);
        throw new InvalidOperationException("A run-services placeholder must not execute tool batches.");
    }
}
