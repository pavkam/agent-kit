// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns one running or settled child process and its bounded output stream.</summary>
public interface IProcessHandle: IAsyncDisposable
{
    /// <summary>Gets the process operation identity.</summary>
    public ProcessOperationId Id { get; }

    /// <summary>Reads bounded standard-output and standard-error events in order.</summary>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>The ordered output event stream.</returns>
    public IAsyncEnumerable<ProcessOutputEvent> ReadOutputAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the terminal completion task shared by all awaiters.</summary>
    public Task<ProcessExitResult> Completion { get; }

    /// <summary>Requests graceful then forced termination according to policy.</summary>
    /// <param name="request">The termination request.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>The termination outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<ProcessTerminationResult> TerminateAsync(
        ProcessTerminationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Writes additional standard-input bytes while the handle uses streaming delivery.</summary>
    /// <param name="data">The bytes to append to the child standard-input stream.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>The write outcome.</returns>
    public ValueTask<ProcessStandardInputWriteResult> WriteStandardInputAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default);

    /// <summary>Completes the standard-input stream when delivery is deferred.</summary>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A task that completes when standard input is closed.</returns>
    public ValueTask CompleteStandardInputAsync(CancellationToken cancellationToken = default);
}
