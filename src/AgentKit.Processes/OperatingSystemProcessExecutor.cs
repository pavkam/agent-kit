// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Starts authorized operating-system processes and returns operation-owned handles.</summary>
internal sealed class OperatingSystemProcessExecutor: IProcessExecutor, IDisposable
{
    private readonly AgentProcessOptionsSnapshot _snapshot;
    private readonly IExecutableResolver _executableResolver;
    private readonly IProcessSandboxSelector _sandboxes;
    private readonly ISecurityGrantStore _grantStore;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly SemaphoreSlim _capacity;

    /// <summary>Initializes an executor for one keyed profile.</summary>
    internal OperatingSystemProcessExecutor(
        AgentProcessOptionsSnapshot snapshot,
        IExecutableResolver executableResolver,
        IProcessSandboxSelector sandboxes,
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(executableResolver);
        ArgumentNullException.ThrowIfNull(sandboxes);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);
        _snapshot = snapshot;
        _executableResolver = executableResolver;
        _sandboxes = sandboxes;
        _grantStore = grantStore;
        _auditDispatcher = auditDispatcher;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
        _intentIds = intentIds;
        var maximumConcurrent = snapshot.OperatingSystem.MaximumConcurrentProcesses;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConcurrent);
        _capacity = new SemaphoreSlim(maximumConcurrent, maximumConcurrent);
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.processes.operating-system");

    /// <summary>Releases executor concurrency resources.</summary>
    public void Dispose() => _capacity.Dispose();

    /// <inheritdoc/>
    public async ValueTask<ProcessStartResult> StartAsync(
        ResolvedProcessStart request,
        SecurityGrant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(grant);
        cancellationToken.ThrowIfCancellationRequested();
        await _capacity.WaitAsync(cancellationToken).ConfigureAwait(false);
        var capacityReleased = false;
        try
        {
            var resolution = await _executableResolver.ResolveAsync(request.Request, cancellationToken).ConfigureAwait(false);
            if (resolution is ExecutableResolutionFailed failed)
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartResolutionFailed(failed.SafeMessage);
            }

            if (resolution is not ExecutableResolved { Resolved: var fresh })
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartDenied("Executable resolution was denied.");
            }

            if (!FactsMatch(request, fresh))
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartResolutionFailed("The resolved process facts changed before creation.");
            }

            var intent = ProcessLegacyIntentMapping.ToResolvedProcessIntent(fresh, _snapshot);
            var sandboxSelection = await _sandboxes.SelectAsync(request.Request.SandboxProfileId, cancellationToken)
                .ConfigureAwait(false);
            if (sandboxSelection is ProcessSandboxMissing)
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartSandboxUnavailable(
                    $"Sandbox profile '{request.Request.SandboxProfileId.Value}' is not registered.");
            }

            if (sandboxSelection is not ProcessSandboxSelected { Provider: var sandbox })
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartSandboxUnavailable("The required sandbox provider could not be selected.");
            }

            var sandboxResult = await sandbox.CreateAsync(
                ProcessSandboxRequestMapping.FromResolvedProcessIntent(intent),
                cancellationToken).ConfigureAwait(false);
            if (sandboxResult.Status != ProcessSandboxStatus.Ready || sandboxResult.Launch is null)
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartSandboxUnavailable(
                    sandboxResult.SafeMessage ?? "The required sandbox could not be enforced.");
            }

            var enforcement = ProcessEnforcementReceipt.Create(
                grant,
                SecurityAudience,
                ProcessSecurityBinding.Resources(intent),
                ProcessSecurityBinding.Fingerprint(intent));
            var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
            var auditFailure = await ProcessHostGuard.ConsumeWithRequiredAuditAsync(
                grant,
                enforcement,
                enforcementIntent,
                _grantStore,
                _auditDispatcher,
                _auditRecordIds,
                _timeProvider,
                cancellationToken).ConfigureAwait(false);
            if (auditFailure is not null)
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartDenied(auditFailure);
            }

            try
            {
                var handle = new OperatingSystemProcessHostSession(
                    intent,
                    sandboxResult.Launch,
                    _timeProvider,
                    _snapshot.OperatingSystem.ForcedTerminationWait,
                    _snapshot.OperatingSystem.MaximumInputBytes,
                    ReleaseCapacity);
                return new ProcessHandleStarted(handle);
            }
            catch (InvalidOperationException)
            {
                ReleaseCapacity();
                capacityReleased = true;
                return new ProcessStartFailed("The operating system refused process creation.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!capacityReleased)
            {
                ReleaseCapacity();
            }

            return new ProcessStartCancelled(SideEffectCertainty.DefinitelyNotPerformed);
        }
    }

    private void ReleaseCapacity() => _capacity.Release();

    private static bool FactsMatch(ResolvedProcessStart expected, ResolvedProcessStart actual) =>
        expected.Executable.AbsolutePath == actual.Executable.AbsolutePath
        && expected.Executable.Fingerprint == actual.Executable.Fingerprint
        && expected.WorkingDirectory == actual.WorkingDirectory
        && expected.EnvironmentFingerprint == actual.EnvironmentFingerprint
        && expected.StandardInputFingerprint == actual.StandardInputFingerprint;
}
