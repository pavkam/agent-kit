// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Runs bounded sandboxed processes with exact grant consumption, output tails, and tree teardown.</summary>
public sealed partial class OperatingSystemProcessRunner: IProcessRunner, IDisposable
{
    private const int _signalTerminate = 15;
    private readonly IProcessIntentResolver _resolver;
    private readonly ImmutableDictionary<SandboxProfileId, IProcessSandboxProvider> _sandboxes;
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _capacity;
    private readonly TimeSpan _forcedTerminationWait;
    private readonly IProcessOutputArtifactSink? _outputArtifacts;
    private readonly int _maximumArtifactOutputBytes;
    private bool _disposed;
    private readonly ILogger<OperatingSystemProcessRunner> _logger;

    /// <summary>Initializes a process runner over exact resolver, sandbox, grant-store, and time boundaries.</summary>
    /// <param name="resolver">The canonical intent resolver used again immediately before creation.</param>
    /// <param name="sandboxes">The additive named sandbox providers.</param>
    /// <param name="grantStore">The authoritative single-use grant store.</param>
    /// <param name="timeProvider">The deterministic timeout and grace-period clock.</param>
    /// <param name="options">The validated host ceilings and concurrency capacity.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <param name="outputArtifacts">The optional complete-output artifact sink.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Sandbox profile identities collide.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured capacity is invalid.</exception>
    public OperatingSystemProcessRunner(
        IProcessIntentResolver resolver,
        IEnumerable<IProcessSandboxProvider> sandboxes,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<OperatingSystemProcessOptions> options,
        ILogger<OperatingSystemProcessRunner>? logger = null,
        IProcessOutputArtifactSink? outputArtifacts = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(sandboxes);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumConcurrentProcesses);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Value.ForcedTerminationWait, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumArtifactOutputBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Value.MaximumArtifactOutputBytes, int.MaxValue);
        _resolver = resolver;
        try
        {
            _sandboxes = sandboxes.ToImmutableDictionary(static sandbox => sandbox.ProfileId);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Sandbox profile identities must be unique.", nameof(sandboxes), exception);
        }

        _grantStore = grantStore;
        _timeProvider = timeProvider;
        var maximumConcurrentProcesses = options.Value.MaximumConcurrentProcesses;
        _capacity = new SemaphoreSlim(maximumConcurrentProcesses, maximumConcurrentProcesses);
        _forcedTerminationWait = options.Value.ForcedTerminationWait;
        _logger = logger ?? NullLogger<OperatingSystemProcessRunner>.Instance;
        _outputArtifacts = outputArtifacts;
        _maximumArtifactOutputBytes = checked((int) options.Value.MaximumArtifactOutputBytes);
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.processes.operating-system");

    /// <inheritdoc/>
    private async ValueTask<ProcessRunResult> RunCoreAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        await _capacity.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var resolution = await _resolver.ResolveAsync(
                request.Intent.Request, cancellationToken).ConfigureAwait(false);
            if (resolution.Status != ProcessResolutionStatus.Resolved || resolution.Intent is null)
            {
                return NotStarted(
                    ProcessRunStatus.ResolutionFailed,
                    resolution.SafeMessage ?? "The process intent could not be revalidated.");
            }

            var currentIntent = resolution.Intent;
            if (ProcessSecurityBinding.Fingerprint(currentIntent) != ProcessSecurityBinding.Fingerprint(request.Intent)
                || !ProcessSecurityBinding.Resources(currentIntent).SequenceEqual(
                    ProcessSecurityBinding.Resources(request.Intent)))
            {
                return NotStarted(ProcessRunStatus.ResolutionFailed, "The resolved process intent changed before creation.");
            }

            if (!_sandboxes.TryGetValue(currentIntent.Request.SandboxProfile, out var sandbox))
            {
                return NotStarted(ProcessRunStatus.SandboxUnavailable, "The required sandbox profile is not registered.");
            }

            var sandboxResult = await sandbox.PrepareAsync(currentIntent, cancellationToken).ConfigureAwait(false);
            if (sandboxResult.Status != ProcessSandboxStatus.Ready || sandboxResult.Launch is null)
            {
                return NotStarted(
                    ProcessRunStatus.SandboxUnavailable,
                    sandboxResult.SafeMessage ?? "The required sandbox could not be enforced.");
            }

            var grantResult = await _grantStore.ValidateAndConsumeAsync(
                request.Grant,
                new SecurityEnforcementRequest(
                    request.Grant.Scope,
                    request.Grant.Identity,
                    SecurityAudience,
                    SecurityOperationKind.Process,
                    SecurityEffect.Execute,
                    ProcessSecurityBinding.Resources(currentIntent),
                    ProcessSecurityBinding.Fingerprint(currentIntent),
                    request.Grant.RevocationVersion),
                cancellationToken).ConfigureAwait(false);
            return grantResult.Status != GrantConsumptionStatus.Consumed
                ? NotStarted(ProcessRunStatus.Denied, grantResult.SafeMessage)
                : await RunCreatedProcessAsync(
                    currentIntent, sandboxResult.Launch, request.Grant, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _capacity.Release();
        }
    }

    /// <summary>Releases concurrency resources after all caller-owned operations have settled.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _capacity.Dispose();
    }

    private async ValueTask<ProcessRunResult> RunCreatedProcessAsync(
        ResolvedProcessIntent intent,
        ProcessSandboxLaunch launch,
        SecurityGrant processGrant,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(intent, launch) };
        try
        {
            if (!process.Start())
            {
                return NotStarted(ProcessRunStatus.Failed, "The operating system did not create the process.");
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return NotStarted(ProcessRunStatus.Failed, "The operating system refused process creation.");
        }

        var standardOutput = new BoundedByteTail(checked((int) intent.Request.Limits.MaximumOutputBytes));
        var standardError = new BoundedByteTail(checked((int) intent.Request.Limits.MaximumOutputBytes));
        var outputCapture = CreateCapture();
        var errorCapture = CreateCapture();
        var outputTask = ReadOutputAsync(process.StandardOutput.BaseStream, standardOutput, outputCapture);
        var errorTask = ReadOutputAsync(process.StandardError.BaseStream, standardError, errorCapture);
        try
        {
            await WriteInputAsync(process, intent.Request.StandardInput, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException)
        {
            var terminated = await TerminateAsync(process, intent.Request.Limits.TerminationGracePeriod)
                .ConfigureAwait(false);
            _ = await DrainOutputAsync(process, outputTask, errorTask, intent.Request.Limits.TerminationGracePeriod)
                .ConfigureAwait(false);
            return await SettledAsync(
                cancellationToken.IsCancellationRequested ? ProcessRunStatus.Cancelled : ProcessRunStatus.Failed,
                standardOutput,
                standardError,
                outputCapture,
                errorCapture,
                intent,
                processGrant,
                terminated
                    ? "Standard input could not be delivered completely."
                    : "Standard input failed and process termination could not be confirmed; effects may continue.",
                CancellationToken.None).ConfigureAwait(false);
        }

        using var timeout = new CancellationTokenSource(intent.Request.Limits.Timeout, _timeProvider);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await process.WaitForExitAsync(operation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var status = cancellationToken.IsCancellationRequested
                ? ProcessRunStatus.Cancelled
                : ProcessRunStatus.TimedOut;
            var terminated = await TerminateAsync(process, intent.Request.Limits.TerminationGracePeriod)
                .ConfigureAwait(false);
            _ = await DrainOutputAsync(process, outputTask, errorTask, intent.Request.Limits.TerminationGracePeriod)
                .ConfigureAwait(false);
            return await SettledAsync(
                status, standardOutput, standardError, outputCapture, errorCapture, intent, processGrant,
                !terminated
                    ? "Process termination could not be confirmed; effects may continue."
                    : status == ProcessRunStatus.TimedOut
                        ? "The process exceeded its operation timeout and was terminated."
                        : "The process was cancelled and terminated.",
                CancellationToken.None).ConfigureAwait(false);
        }

        var drained = await DrainOutputAsync(
            process, outputTask, errorTask, intent.Request.Limits.TerminationGracePeriod).ConfigureAwait(false);
        return !drained
            ? await SettledAsync(
                ProcessRunStatus.Failed,
                standardOutput,
                standardError,
                outputCapture,
                errorCapture,
                intent,
                processGrant,
                "The process exited, but inherited output streams did not settle within the drain bound.",
                cancellationToken).ConfigureAwait(false)
            : await CreateResultAsync(
                ProcessRunStatus.Exited,
                process.ExitCode,
                standardOutput,
                standardError,
                outputCapture,
                errorCapture,
                ProcessSideEffectCertainty.Completed,
                null,
                intent,
                processGrant,
                cancellationToken).ConfigureAwait(false);
    }

    private static ProcessStartInfo CreateStartInfo(
        ResolvedProcessIntent intent,
        ProcessSandboxLaunch launch)
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

    private static async Task WriteInputAsync(
        Process process,
        ImmutableArray<byte> input,
        CancellationToken cancellationToken)
    {
        if (!input.IsEmpty)
        {
            await process.StandardInput.BaseStream.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        process.StandardInput.Close();
    }

    private static async Task<bool> ReadOutputAsync(Stream stream, BoundedByteTail tail, BoundedByteCapture? capture)
    {
        var buffer = new byte[81920];
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                if (read == 0)
                {
                    return true;
                }

                tail.Append(buffer.AsSpan(0, read));
                capture?.Append(buffer.AsSpan(0, read));
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private async Task<bool> DrainOutputAsync(
        Process process,
        Task<bool> outputTask,
        Task<bool> errorTask,
        TimeSpan drainBound)
    {
        var both = Task.WhenAll(outputTask, errorTask);
        if (both.IsCompleted)
        {
            var completed = await both.ConfigureAwait(false);
            return completed.All(static succeeded => succeeded);
        }

        var delay = Task.Delay(drainBound, _timeProvider, CancellationToken.None);
        if (await Task.WhenAny(both, delay).ConfigureAwait(false) == both)
        {
            var completed = await both.ConfigureAwait(false);
            return completed.All(static succeeded => succeeded);
        }

        _ = TryKillTree(process);
        process.StandardOutput.Close();
        process.StandardError.Close();
        _ = await both.ConfigureAwait(false);
        return false;
    }

    private async Task<bool> TerminateAsync(Process process, TimeSpan gracePeriod)
    {
        if (process.HasExited)
        {
            return true;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _ = Kill(process.Id, _signalTerminate);
        }

        if (await WaitForExitWithinAsync(process, gracePeriod).ConfigureAwait(false))
        {
            return true;
        }

        _ = TryKillTree(process);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            _ = Kill(process.Id, 9);
        }

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
            : await Task.WhenAny(
                exit,
                Task.Delay(duration, _timeProvider, CancellationToken.None)).ConfigureAwait(false) == exit;
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

    private static ProcessRunResult NotStarted(ProcessRunStatus status, string message) => new(
        status,
        null,
        [],
        [],
        0,
        0,
        false,
        false,
        ProcessSideEffectCertainty.NotStarted,
        message);

    private Task<ProcessRunResult> SettledAsync(
        ProcessRunStatus status,
        BoundedByteTail standardOutput,
        BoundedByteTail standardError,
        BoundedByteCapture? outputCapture,
        BoundedByteCapture? errorCapture,
        ResolvedProcessIntent intent,
        SecurityGrant processGrant,
        string message,
        CancellationToken cancellationToken) => CreateResultAsync(
            status,
            null,
            standardOutput,
            standardError,
            outputCapture,
            errorCapture,
            ProcessSideEffectCertainty.MayHaveOccurred,
            message,
            intent,
            processGrant,
            cancellationToken);

    private BoundedByteCapture? CreateCapture() => _outputArtifacts is null
        ? null
        : new BoundedByteCapture(_maximumArtifactOutputBytes);

    private async Task<ProcessRunResult> CreateResultAsync(
        ProcessRunStatus status,
        int? exitCode,
        BoundedByteTail standardOutput,
        BoundedByteTail standardError,
        BoundedByteCapture? outputCapture,
        BoundedByteCapture? errorCapture,
        ProcessSideEffectCertainty certainty,
        string? safeMessage,
        ResolvedProcessIntent intent,
        SecurityGrant processGrant,
        CancellationToken cancellationToken)
    {
        var (outputArtifact, outputFailed) = await StoreOutputAsync(
            intent, processGrant, ProcessOutputKind.StandardOutput, standardOutput, outputCapture, cancellationToken).ConfigureAwait(false);
        var (errorArtifact, errorFailed) = await StoreOutputAsync(
            intent, processGrant, ProcessOutputKind.StandardError, standardError, errorCapture, cancellationToken).ConfigureAwait(false);
        if (outputFailed || errorFailed)
        {
            safeMessage = string.IsNullOrWhiteSpace(safeMessage)
                ? "One or more truncated output streams could not be preserved completely."
                : $"{safeMessage} One or more truncated output streams could not be preserved completely.";
        }

        return new ProcessRunResult(
            status,
            exitCode,
            standardOutput.ToImmutableArray(),
            standardError.ToImmutableArray(),
            standardOutput.TotalBytes,
            standardError.TotalBytes,
            standardOutput.IsTruncated,
            standardError.IsTruncated,
            certainty,
            safeMessage,
            outputArtifact,
            errorArtifact);
    }

    private async Task<(ArtifactReference? Reference, bool Failed)> StoreOutputAsync(
        ResolvedProcessIntent intent,
        SecurityGrant processGrant,
        ProcessOutputKind kind,
        BoundedByteTail tail,
        BoundedByteCapture? capture,
        CancellationToken cancellationToken)
    {
        if (!tail.IsTruncated)
        {
            return (null, false);
        }

        if (_outputArtifacts is null || capture is null || !capture.IsComplete)
        {
            return (null, true);
        }

        try
        {
            var result = await _outputArtifacts.StoreAsync(new ProcessOutputArtifactRequest(
                intent,
                processGrant.Scope,
                processGrant.Identity,
                kind,
                capture.ToImmutableArray(),
                new IdempotencyKey($"process-output:{intent.Request.Id}:{kind}")), cancellationToken).ConfigureAwait(false);
            return result is ProcessOutputArtifactStored stored
                ? (stored.Reference, false)
                : (null, true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return (null, true);
        }
    }

    [LibraryImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static partial int Kill(int processId, int signal);
}
