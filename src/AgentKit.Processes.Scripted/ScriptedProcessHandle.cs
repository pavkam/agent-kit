// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

using System.Threading.Channels;

/// <summary>Streams declared output for one scripted process operation.</summary>
internal sealed class ScriptedProcessHandle: IProcessHandle
{
    private readonly Channel<ProcessOutputEvent> _events;
    private int _disposed;

    /// <summary>Initializes one scripted handle that publishes scenario output asynchronously.</summary>
    /// <param name="operationId">The process operation identity.</param>
    /// <param name="scenario">The declared scenario behavior.</param>
    /// <param name="timeProvider">The injected clock for declared delays.</param>
    /// <param name="cancellationToken">Propagates caller cancellation into the simulated lifecycle.</param>
    internal ScriptedProcessHandle(
        ProcessOperationId operationId,
        ScriptedProcessScenario scenario,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId.Value, Guid.Empty, nameof(operationId));
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Id = operationId;
        _events = Channel.CreateUnbounded<ProcessOutputEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });
        Completion = RunScenarioAsync(scenario, timeProvider, cancellationToken);
    }

    /// <inheritdoc/>
    public ProcessOperationId Id { get; }

    /// <inheritdoc/>
    public Task<ProcessExitResult> Completion { get; }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ProcessOutputEvent> ReadOutputAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await foreach (var item in _events.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public ValueTask<ProcessTerminationResult> TerminateAsync(
        ProcessTerminationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ProcessTerminationResult(true, SideEffectCertainty.DefinitelyPerformed));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _ = _events.Writer.TryComplete();
        try
        {
            _ = await Completion.ConfigureAwait(false);
        }
        catch
        {
            // Disposal observes terminal completion without surfacing scenario faults.
        }
    }

    private async Task<ProcessExitResult> RunScenarioAsync(
        ScriptedProcessScenario scenario,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            if (scenario.DelayAfterStart > TimeSpan.Zero)
            {
                await Task.Delay(scenario.DelayAfterStart, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            var sequence = 0L;
            if (scenario.StandardOutput.Length > 0)
            {
                await WriteEventAsync(new ProcessStandardOutputBytes(sequence++, scenario.StandardOutput.AsMemory())).ConfigureAwait(false);
            }

            if (scenario.StandardError.Length > 0)
            {
                await WriteEventAsync(new ProcessStandardErrorBytes(sequence++, scenario.StandardError.AsMemory())).ConfigureAwait(false);
            }

            if (scenario.StandardOutputTruncated)
            {
                await WriteEventAsync(new ProcessStandardOutputTruncated(sequence++, scenario.TotalStandardOutputBytes))
                    .ConfigureAwait(false);
            }

            if (scenario.StandardErrorTruncated)
            {
                await WriteEventAsync(new ProcessStandardErrorTruncated(sequence++, scenario.TotalStandardErrorBytes))
                    .ConfigureAwait(false);
            }

            await WriteEventAsync(new ProcessOutputStreamsCompleted(sequence)).ConfigureAwait(false);
            _ = _events.Writer.TryComplete();
            return scenario.Exit;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = _events.Writer.TryComplete();
            return new ProcessCancelled(SideEffectCertainty.Unknown);
        }
    }

    private ValueTask WriteEventAsync(ProcessOutputEvent evt) => _events.Writer.WriteAsync(evt);
}
