// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Consumes exact observation grants and returns declared language snapshots without host effects.</summary>
public sealed partial class ScriptedLanguageIntelligenceService: ILanguageIntelligenceService
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly TimeProvider _timeProvider;
    private readonly ImmutableDictionary<LanguageQueryId, ScriptedLanguageScenario> _scenarios;
    private readonly ILogger<ScriptedLanguageIntelligenceService> _logger;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;

    /// <summary>Initializes the deterministic provider from captured scenarios and enforcement dependencies.</summary>
    /// <param name="grantStore">The authoritative exact single-use grant store.</param>
    /// <param name="timeProvider">The clock used for declared delays.</param>
    /// <param name="options">The captured scenario configuration.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">Scenario query identities collide.</exception>
    public ScriptedLanguageIntelligenceService(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<ScriptedLanguageOptions> options)
        : this(
            grantStore,
            timeProvider,
            options,
            NullLogger<ScriptedLanguageIntelligenceService>.Instance,
            new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the deterministic provider with content-free Microsoft logging.</summary>
    /// <param name="grantStore">The authoritative exact single-use grant store.</param>
    /// <param name="timeProvider">The clock used for declared delays.</param>
    /// <param name="options">The captured scenario configuration.</param>
    /// <param name="logger">The logger receiving safe structural lifecycle events.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">Scenario query identities collide.</exception>
    public ScriptedLanguageIntelligenceService(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<ScriptedLanguageOptions> options,
        ILogger<ScriptedLanguageIntelligenceService> logger)
        : this(grantStore, timeProvider, options, logger, new GuidSecurityEnforcementIntentIdGenerator())
    {
    }

    /// <summary>Initializes the deterministic provider with a replaceable source of fresh enforcement-intent identities.</summary>
    /// <param name="grantStore">The non-null authoritative exact single-use grant store.</param>
    /// <param name="timeProvider">The non-null clock used for declared delays.</param>
    /// <param name="options">The non-null captured scenario configuration.</param>
    /// <param name="logger">The non-null logger receiving safe structural lifecycle events.</param>
    /// <param name="intentIds">The non-null thread-safe source of distinct atomic permission-to-begin identities.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException">Scenario query identities collide.</exception>
    public ScriptedLanguageIntelligenceService(
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        IOptions<ScriptedLanguageOptions> options,
        ILogger<ScriptedLanguageIntelligenceService> logger,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(intentIds);
        _grantStore = grantStore;
        _timeProvider = timeProvider;
        _logger = logger;
        _intentIds = intentIds;
        try
        {
            _scenarios = options.Value.Scenarios.ToImmutableDictionary(static scenario => scenario.QueryId);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Scripted language query identities must be unique.", nameof(options), exception);
        }
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; } = new("agentkit.language.scripted");

    /// <inheritdoc/>
    private async ValueTask<LanguageQueryResult> QueryCoreAsync(
        LanguageQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_scenarios.TryGetValue(request.Id, out var scenario))
        {
            return Failure(request.Kind, LanguageQueryStatus.Unavailable, "No scripted language scenario is configured.");
        }

        if (scenario.Result.Kind != request.Kind)
        {
            return Failure(request.Kind, LanguageQueryStatus.Failed, "The scripted result does not match the requested operation.");
        }

        var enforcement = LanguageEnforcementReceipt.Create(
            request.Grant,
            SecurityAudience,
            request.Kind,
            request.Path,
            LanguageSecurityBinding.Fingerprint(request));
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        var consumption = await _grantStore.ValidateAndConsumeAsync(
            request.Grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!LanguageEnforcementReceipt.IsFreshExact(consumption, request.Grant, enforcement, intent))
        {
            return Failure(request.Kind, LanguageQueryStatus.Denied, LanguageEnforcementReceipt.DenialMessage(consumption));
        }

        try
        {
            if (scenario.DelayAfterAdmission > TimeSpan.Zero)
            {
                await Task.Delay(scenario.DelayAfterAdmission, _timeProvider, cancellationToken).ConfigureAwait(false);
            }

            return Bound(scenario.Result, request.MaximumResults);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                request.Kind,
                LanguageQueryStatus.Cancelled,
                "The language query was cancelled after protected observation may have started.");
        }
    }

    private static LanguageQueryResult Failure(
        LanguageQueryKind kind,
        LanguageQueryStatus status,
        string message) => new(status, kind, null, [], [], [], false, message);

    private static LanguageQueryResult Bound(LanguageQueryResult result, int maximumResults)
    {
        ImmutableArray<LanguageLocation> locations = [.. result.Locations.Take(maximumResults)];
        ImmutableArray<LanguageSymbol> symbols = [.. result.Symbols.Take(maximumResults)];
        ImmutableArray<LanguageDiagnostic> diagnostics = [.. result.Diagnostics.Take(maximumResults)];
        var complete = result.Complete
            && locations.Length == result.Locations.Length
            && symbols.Length == result.Symbols.Length
            && diagnostics.Length == result.Diagnostics.Length;
        return new LanguageQueryResult(
            result.Status,
            result.Kind,
            result.HoverText,
            locations,
            symbols,
            diagnostics,
            complete,
            result.SafeMessage);
    }
}
