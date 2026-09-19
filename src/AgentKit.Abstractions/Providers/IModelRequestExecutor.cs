// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs one captured model selection, including same-model retry.</summary>
/// <remarks>
/// The executor does not select a fallback model. When same-model retry is
/// exhausted for a retryable failure it returns <see cref="ModelFallbackRequired"/>
/// and leaves reselection to the loop. It returns <see cref="Task"/> because
/// network execution is inherently asynchronous. Observer cancellation and the
/// caller's <see cref="CancellationToken"/> stay distinct; cancellation is not
/// retried.
/// </remarks>
public interface IModelRequestExecutor
{
    /// <summary>Executes one captured request.</summary>
    /// <param name="request">The captured operation, selection, context, and retry bound.</param>
    /// <param name="observer">The observer that receives each attempt event before the next one is emitted.</param>
    /// <param name="cancellationToken">Cancels the execution. Cancellation is not retried.</param>
    /// <returns>The closed execution outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="observer"/> is null.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before a terminal outcome was produced.
    /// A canceled attempt that already has a terminal outcome returns <see cref="ModelExecutionCancelled"/> instead.
    /// </exception>
    public Task<ModelExecutionResult> ExecuteAsync(
        ModelExecutionRequest request,
        IModelResponseObserver observer,
        CancellationToken cancellationToken = default);
}
