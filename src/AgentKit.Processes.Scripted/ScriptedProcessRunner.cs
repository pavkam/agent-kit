// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Consumes exact process grants and returns declared results without creating operating-system processes.</summary>
public sealed partial class ScriptedProcessRunner: IProcessRunner
{
    private readonly IProcessIntentResolver _resolver;
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly ImmutableDictionary<ProcessOperationId, ScriptedProcessScenario> _scenarios;
    private readonly ILogger<ScriptedProcessRunner> _logger;

    /// <summary>Initializes a deterministic runner from captured scenarios and enforcement dependencies.</summary>
    /// <param name="resolver">The paired deterministic resolver used again before simulated start.</param>
    /// <param name="grantStore">The authoritative exact single-use grant store.</param>
    /// <param name="timeProvider">The clock used for declared post-start delays.</param>
    /// <param name="options">The configured operation scenarios.</param>
    /// <param name="logger">The optional structured logger; a null value disables log publication.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">Scenario operation identities collide.</exception>
    public ScriptedProcessRunner(
        IProcessIntentResolver resolver,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<ScriptedProcessOptions> options,
        ILogger<ScriptedProcessRunner>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _resolver = resolver;
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        try
        {
            _scenarios = options.Value.Scenarios.ToImmutableDictionary(static item => item.OperationId);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Scripted process operation identities must be unique.", nameof(options), exception);
        }

        _logger = logger ?? NullLogger<ScriptedProcessRunner>.Instance;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.processes.scripted");

    /// <inheritdoc/>
    private async ValueTask<ProcessRunResult> RunCoreAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var resolution = await _resolver.ResolveAsync(request.Intent.Request, cancellationToken).ConfigureAwait(false);
        if (resolution.Status != ProcessResolutionStatus.Resolved
            || resolution.Intent is null
            || ProcessSecurityBinding.Fingerprint(resolution.Intent) != ProcessSecurityBinding.Fingerprint(request.Intent)
            || !ProcessSecurityBinding.Resources(resolution.Intent).SequenceEqual(ProcessSecurityBinding.Resources(request.Intent)))
        {
            return NotStarted(ProcessRunStatus.ResolutionFailed, "The scripted process intent changed before start.");
        }

        if (!_scenarios.TryGetValue(request.Intent.Request.Id, out var scenario))
        {
            return NotStarted(ProcessRunStatus.Failed, "No scripted process scenario is configured for the operation.");
        }

        var grant = await _grantStore.ValidateAndConsumeAsync(
            request.Grant,
            new SecurityEnforcementRequest(
                request.Grant.Scope,
                request.Grant.Identity,
                SecurityAudience,
                SecurityOperationKind.Process,
                SecurityEffect.Execute,
                ProcessSecurityBinding.Resources(resolution.Intent),
                ProcessSecurityBinding.Fingerprint(resolution.Intent),
                request.Grant.RevocationVersion),
            cancellationToken).ConfigureAwait(false);
        if (grant.Status != GrantConsumptionStatus.Consumed)
        {
            return NotStarted(ProcessRunStatus.Denied, grant.SafeMessage);
        }

        try
        {
            if (scenario.DelayAfterStart > TimeSpan.Zero)
            {
                await Task.Delay(scenario.DelayAfterStart, _timeProvider, cancellationToken).ConfigureAwait(false);
            }

            return scenario.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ProcessRunResult(
                ProcessRunStatus.Cancelled,
                null,
                [],
                [],
                0,
                0,
                false,
                false,
                ProcessSideEffectCertainty.MayHaveOccurred,
                "The scripted process was cancelled after simulated start.");
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
}
