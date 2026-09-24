// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Consumes exact process grants and returns scripted operation handles.</summary>
public sealed partial class ScriptedProcessExecutor: IProcessExecutor
{
    private readonly ScriptedProcessOptionsSnapshot _snapshot;
    private readonly IExecutableResolver _executableResolver;
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly ILogger<ScriptedProcessExecutor> _logger;

    /// <summary>Initializes a deterministic executor from captured scenarios and enforcement dependencies.</summary>
    /// <param name="snapshot">The immutable keyed profile snapshot.</param>
    /// <param name="executableResolver">The paired resolver used again before simulated start.</param>
    /// <param name="grantStore">The authoritative exact single-use grant store.</param>
    /// <param name="timeProvider">The clock used for declared post-start delays.</param>
    /// <param name="intentIds">The non-null thread-safe source of fresh per-process enforcement intent identities.</param>
    /// <param name="logger">The optional structured logger; a null value selects a null logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    internal ScriptedProcessExecutor(
        ScriptedProcessOptionsSnapshot snapshot,
        IExecutableResolver executableResolver,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        ILogger<ScriptedProcessExecutor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(executableResolver);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(intentIds);
        _snapshot = snapshot;
        _executableResolver = executableResolver;
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _intentIds = intentIds;
        _logger = logger ?? NullLogger<ScriptedProcessExecutor>.Instance;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.processes.scripted");

    /// <inheritdoc/>
    private async ValueTask<ProcessStartResult> StartCoreAsync(
        ResolvedProcessStart request,
        SecurityGrant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(grant);
        cancellationToken.ThrowIfCancellationRequested();
        var resolution = await _executableResolver.ResolveAsync(request.Request, cancellationToken).ConfigureAwait(false);
        if (resolution is ExecutableResolutionFailed failed)
        {
            return new ProcessStartResolutionFailed(failed.SafeMessage);
        }

        if (resolution is not ExecutableResolved { Resolved: var fresh })
        {
            return new ProcessStartDenied("Executable resolution was denied.");
        }

        if (!FactsMatch(request, fresh))
        {
            return new ProcessStartResolutionFailed("The resolved process facts changed before creation.");
        }

        if (!_snapshot.Scenarios.TryGetValue(request.Request.Id, out var scenario))
        {
            return new ProcessStartFailed("No scripted process scenario is configured for the operation.");
        }

        var intent = ProcessStartBinding.ToResolvedProcessIntent(fresh);
        var enforcement = ProcessEnforcementReceipt.Create(
            grant,
            SecurityAudience,
            ProcessSecurityBinding.Resources(intent),
            ProcessSecurityBinding.Fingerprint(intent));
        var enforcementIntent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var grantResult = await _grantStore.ValidateAndConsumeAsync(
            grant,
            enforcement,
            enforcementIntent,
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ProcessEnforcementReceipt.IsFreshExact(grantResult, grant, enforcement, enforcementIntent))
        {
            return new ProcessStartDenied(
                grantResult.Status == GrantConsumptionStatus.Consumed
                    ? "The grant store did not retain a fresh exact enforcement-intent receipt."
                    : grantResult.SafeMessage ?? "The process start grant was not accepted.");
        }

        var handle = new ScriptedProcessHandle(request.Request.Id, scenario, _timeProvider, cancellationToken);
        return new ProcessHandleStarted(handle);
    }

    private static bool FactsMatch(ResolvedProcessStart expected, ResolvedProcessStart actual) =>
        expected.Executable.AbsolutePath == actual.Executable.AbsolutePath
        && expected.Executable.Fingerprint == actual.Executable.Fingerprint
        && expected.WorkingDirectory == actual.WorkingDirectory
        && expected.EnvironmentFingerprint == actual.EnvironmentFingerprint
        && expected.StandardInputFingerprint == actual.StandardInputFingerprint;
}
