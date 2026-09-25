// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;

/// <summary>Owns one operating-system child process and streams bounded output events.</summary>
internal sealed partial class OperatingSystemProcessHostSession: IProcessHandle
{
    private const int _signalTerminate = 15;
    private readonly Process _process;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _forcedTerminationWait;
    private readonly Channel<ProcessOutputEvent> _events;
    private Task<ProcessExitResult> RunCompletion { get; }
    private readonly Action? _releaseCapacity;
    private readonly long _maximumInputBytes;
    private readonly ProcessStandardInputDelivery _standardInputDelivery;
    private readonly Lock _stdinGate = new();
    private long _nextSequence;
    private long _stdinBytesWritten;
    private int _stdinCompleted;
    private int _disposed;

    /// <summary>Starts one sandboxed child and begins streaming output.</summary>
    internal OperatingSystemProcessHostSession(
        ResolvedProcessIntent intent,
        ProcessSandboxLaunch launch,
        TimeProvider timeProvider,
        TimeSpan forcedTerminationWait,
        long maximumInputBytes,
        Action? releaseCapacity = null)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(forcedTerminationWait, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumInputBytes);
        Id = intent.Request.Id;
        _timeProvider = timeProvider;
        _forcedTerminationWait = forcedTerminationWait;
        _maximumInputBytes = maximumInputBytes;
        _standardInputDelivery = intent.Request.Limits.StandardInputDelivery;
        _releaseCapacity = releaseCapacity;
        _events = Channel.CreateUnbounded<ProcessOutputEvent>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        _process = new Process { StartInfo = CreateStartInfo(intent, launch) };
        if (!_process.Start())
        {
            throw new InvalidOperationException("The operating system did not create the process.");
        }

        RunCompletion = RunLifecycleAsync(intent);
    }

    /// <inheritdoc/>
    public ProcessOperationId Id { get; }

    /// <inheritdoc/>
    public Task<ProcessExitResult> Completion => RunCompletion;

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
    public async ValueTask<ProcessTerminationResult> TerminateAsync(
        ProcessTerminationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        var completed = await TerminateProcessAsync(_process, intentGrace: TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        return new ProcessTerminationResult(
            completed,
            completed ? SideEffectCertainty.DefinitelyPerformed : SideEffectCertainty.Unknown);
    }

    /// <inheritdoc/>
    public ValueTask<ProcessStandardInputWriteResult> WriteStandardInputAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_standardInputDelivery != ProcessStandardInputDelivery.Streaming)
        {
            return ValueTask.FromResult<ProcessStandardInputWriteResult>(
                new ProcessStandardInputWriteUnavailable("Standard input is not open for streaming on this handle."));
        }

        if (Volatile.Read(ref _stdinCompleted) != 0)
        {
            return ValueTask.FromResult<ProcessStandardInputWriteResult>(
                new ProcessStandardInputWriteUnavailable("Standard input is already completed."));
        }

        if (data.IsEmpty)
        {
            return ValueTask.FromResult<ProcessStandardInputWriteResult>(new ProcessStandardInputWriteSucceeded(0));
        }

        lock (_stdinGate)
        {
            if (Volatile.Read(ref _stdinCompleted) != 0)
            {
                return ValueTask.FromResult<ProcessStandardInputWriteResult>(
                    new ProcessStandardInputWriteUnavailable("Standard input is already completed."));
            }

            if (_stdinBytesWritten + data.Length > _maximumInputBytes)
            {
                return ValueTask.FromResult<ProcessStandardInputWriteResult>(
                    new ProcessStandardInputWriteLimitExceeded("Standard input exceeds the configured maximum."));
            }
        }

        return WriteStandardInputCoreAsync(data, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask CompleteStandardInputAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_standardInputDelivery != ProcessStandardInputDelivery.Streaming)
        {
            return;
        }

        await CloseStandardInputAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        await CloseStandardInputAsync().ConfigureAwait(false);
        _ = await TerminateProcessAsync(_process, intentGrace: TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        _ = _events.Writer.TryComplete();
        _process.Dispose();
        _releaseCapacity?.Invoke();
    }

    private async Task<ProcessExitResult> RunLifecycleAsync(ResolvedProcessIntent intent)
    {
        var maximumOutputBytes = checked((int) intent.Request.Limits.MaximumOutputBytes);
        var stdout = new BoundedByteTail(maximumOutputBytes);
        var stderr = new BoundedByteTail(maximumOutputBytes);
        var stdoutTruncated = false;
        var stderrTruncated = false;
        var outputTask = PumpStreamAsync(_process.StandardOutput.BaseStream, stdout, ProcessOutputKind.StandardOutput, () => stdoutTruncated = true);
        var errorTask = PumpStreamAsync(_process.StandardError.BaseStream, stderr, ProcessOutputKind.StandardError, () => stderrTruncated = true);
        using var timeout = new CancellationTokenSource(intent.Request.Limits.Timeout, _timeProvider);
        try
        {
            await WriteInitialStandardInputAsync(intent.Request.StandardInput, timeout.Token).ConfigureAwait(false);
            await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var certainty = timeout.IsCancellationRequested
                ? SideEffectCertainty.Unknown
                : SideEffectCertainty.Unknown;
            _ = await TerminateProcessAsync(_process, intent.Request.Limits.TerminationGracePeriod).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            await CompleteEventsAsync().ConfigureAwait(false);
            return timeout.IsCancellationRequested
                ? new ProcessTimedOut(certainty)
                : new ProcessCancelled(certainty);
        }
        catch (Exception)
        {
            _ = await TerminateProcessAsync(_process, intent.Request.Limits.TerminationGracePeriod).ConfigureAwait(false);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            await CompleteEventsAsync().ConfigureAwait(false);
            return new ProcessUnknownExit(SideEffectCertainty.Unknown);
        }

        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        await CompleteEventsAsync().ConfigureAwait(false);
        return _process.HasExited
            ? new ProcessExited(_process.ExitCode)
            : new ProcessUnknownExit(SideEffectCertainty.Unknown);

        async Task CompleteEventsAsync()
        {
            if (stdoutTruncated)
            {
                await WriteEventAsync(new ProcessStandardOutputTruncated(NextSequence(), stdout.TotalBytes)).ConfigureAwait(false);
            }

            if (stderrTruncated)
            {
                await WriteEventAsync(new ProcessStandardErrorTruncated(NextSequence(), stderr.TotalBytes)).ConfigureAwait(false);
            }

            await WriteEventAsync(new ProcessOutputStreamsCompleted(NextSequence())).ConfigureAwait(false);
            _events.Writer.Complete();
        }
    }

    private async Task PumpStreamAsync(
        Stream stream,
        BoundedByteTail tail,
        ProcessOutputKind kind,
        Action onTruncated)
    {
        var buffer = new byte[81920];
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                if (read == 0)
                {
                    return;
                }

                var wasTruncated = tail.IsTruncated;
                tail.Append(buffer.AsSpan(0, read));
                if (!wasTruncated && tail.IsTruncated)
                {
                    onTruncated();
                }

                var chunk = buffer.AsMemory(0, read);
                ProcessOutputEvent evt = kind == ProcessOutputKind.StandardOutput
                    ? new ProcessStandardOutputBytes(NextSequence(), chunk)
                    : new ProcessStandardErrorBytes(NextSequence(), chunk);
                await WriteEventAsync(evt).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }

    private async ValueTask WriteEventAsync(ProcessOutputEvent evt) =>
        await _events.Writer.WriteAsync(evt, CancellationToken.None).ConfigureAwait(false);

    private long NextSequence() => Interlocked.Increment(ref _nextSequence) - 1;

    private static ProcessStartInfo CreateStartInfo(ResolvedProcessIntent intent, ProcessSandboxLaunch launch)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = launch.ExecutablePath,
            WorkingDirectory = intent.AbsoluteWorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.Environment.Clear();
        foreach (var variable in intent.Request.Environment)
        {
            startInfo.Environment.Add(variable.Name, variable.Value);
        }

        foreach (var argument in launch.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private async Task WriteInitialStandardInputAsync(
        ImmutableArray<byte> input,
        CancellationToken cancellationToken)
    {
        if (!input.IsEmpty)
        {
            var write = await WriteStandardInputCoreAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (write is not ProcessStandardInputWriteSucceeded)
            {
                throw new InvalidOperationException("The initial standard-input payload could not be written.");
            }
        }

        if (_standardInputDelivery == ProcessStandardInputDelivery.AfterInitialPayload)
        {
            await CloseStandardInputAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<ProcessStandardInputWriteResult> WriteStandardInputCoreAsync(
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        try
        {
            await _process.StandardInput.BaseStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await _process.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _ = Interlocked.Add(ref _stdinBytesWritten, data.Length);
            return new ProcessStandardInputWriteSucceeded(data.Length);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new ProcessStandardInputWriteUnavailable("Standard input is no longer writable.");
        }
    }

    private ValueTask CloseStandardInputAsync()
    {
        if (Interlocked.CompareExchange(ref _stdinCompleted, 1, 0) != 0)
        {
            return ValueTask.CompletedTask;
        }

        lock (_stdinGate)
        {
            try
            {
                _process.StandardInput.Close();
            }
            catch (Exception)
            {
                // Closing stdin is best-effort once the process has already exited.
            }
        }

        return ValueTask.CompletedTask;
    }

    private async Task<bool> TerminateProcessAsync(Process process, TimeSpan intentGrace)
    {
        if (process.HasExited)
        {
            return true;
        }

        if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && !process.HasExited)
        {
            _ = Kill(process.Id, _signalTerminate);
        }

        if (await WaitForExitWithinAsync(process, intentGrace).ConfigureAwait(false))
        {
            return true;
        }

        _ = TryKillTree(process);
        return await WaitForExitWithinAsync(process, _forcedTerminationWait).ConfigureAwait(false);
    }

    private async Task<bool> WaitForExitWithinAsync(Process process, TimeSpan duration)
    {
        if (process.HasExited)
        {
            return true;
        }

        var exit = process.WaitForExitAsync();
        return duration == TimeSpan.Zero
            ? exit.IsCompleted
            : await Task.WhenAny(exit, Task.Delay(duration, _timeProvider, CancellationToken.None)).ConfigureAwait(false) == exit;
    }

    private static bool TryKillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int processId, int signal);
}
