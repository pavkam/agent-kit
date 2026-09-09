// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Provides the first-party budget runtime over an explicitly selected authoritative ledger.</summary>
/// <remarks>The singleton owns no accounting state. Returned scopes are immutable handles over persisted references.</remarks>
internal sealed class BudgetAuthority: IBudgetAuthority
{
    private readonly IBudgetLedger _ledger;
    private readonly AgentBudgetOptionsSnapshot _options;
    private readonly ILogger<BudgetAuthority> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Initializes the runtime over the application-selected ledger.</summary>
    /// <param name="ledger">The singular authoritative ledger selected by application composition.</param>
    /// <param name="options">The validated admission policy captured for newly created scopes.</param>
    /// <param name="loggerFactory">The optional structured logger factory; null disables log publication.</param>
    /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
    public BudgetAuthority(IBudgetLedger ledger, AgentBudgetOptionsSnapshot options, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(options);
        _ledger = ledger;
        _options = options;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = CreateLogger<BudgetAuthority>(_loggerFactory);
    }

    /// <inheritdoc/>
    public async ValueTask<BudgetScopeResult> CreateChildScopeAsync(
        BudgetScopeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var admission = new BudgetScopeAdmission(
            _options.MaximumScopeDepth,
            _options.MaximumOpenReservationsPerScope,
            _options.DefaultReservationLifetime,
            _options.OverrunBehavior switch
            {
                BudgetOverrunBehavior.RecordAndBlockFurtherReservations => BudgetOverrunHoldPolicy.ClearWhenReconciled,
                BudgetOverrunBehavior.RequireOperatorReconciliation => BudgetOverrunHoldPolicy.RequireAuthorizedResolution,
                _ => throw new UnreachableException(),
            });
        var ledgerRequest = new BudgetLedgerScopeCreateRequest(request, admission);
        using var observation = BudgetRuntimeObservation.Start(AgentKitActivityNames.BudgetScopeCreate);
        observation.Tag(AgentKitTagNames.AgentId, request.Address.AgentId.ToString());
        observation.Tag(AgentKitTagNames.SessionId, request.Address.SessionId?.ToString());
        observation.Tag(AgentKitTagNames.RunId, request.Address.RunId?.ToString());
        observation.Tag(AgentKitTagNames.OperationId, request.Address.OperationId?.ToString());
        try
        {
            var result = await _ledger.CreateScopeAsync(ledgerRequest, cancellationToken).ConfigureAwait(false);
            BudgetScopeResult mapped = result switch
            {
                BudgetLedgerScopeCreated created => new BudgetScopeCreated(
                    new BudgetScope(
                        _ledger,
                        created.Scope,
                        CreateLogger<BudgetScope>(_loggerFactory),
                        CreateLogger<BudgetReservation>(_loggerFactory))),
                BudgetLedgerScopeCreateRejected rejected => rejected.Failure,
                _ => throw new UnreachableException(),
            };
            if (mapped is BudgetScopeCreated scopeCreated)
            {
                observation.Tag(AgentKitTagNames.BudgetScopeId, scopeCreated.Scope.Id.ToString());
                observation.Success("created");
                TryLog(() => BudgetLog.ScopeCreated(_logger, scopeCreated.Scope.Id, "created"));
                TryMetric("created");
            }
            else
            {
                var failure = (BudgetScopeCreationFailed) mapped;
                observation.Rejected("rejected");
                TryLog(() => BudgetLog.ScopeCreationRejected(_logger, failure.Kind.ToString()));
                TryMetric("rejected");
            }
            return mapped;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            observation.Failure("cancelled", nameof(OperationCanceledException));
            TryLog(() => BudgetLog.ScopeCreationCancelled(_logger));
            TryMetric("cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            observation.Failure("failed", errorType);
            TryLog(() => BudgetLog.ScopeCreationFailed(_logger, errorType));
            TryMetric("failed");
            throw;
        }
    }

    private static void TryLog(Action publish)
    {
        Debug.Assert(publish is not null, "Callers provide a diagnostic publication action.");
        try { publish(); } catch (Exception) { }
    }

    private static ILogger<T> CreateLogger<T>(ILoggerFactory loggerFactory)
    {
        Debug.Assert(loggerFactory is not null, "The constructor established a logger factory.");
        try { return loggerFactory.CreateLogger<T>(); } catch (Exception) { return NullLogger<T>.Instance; }
    }

    private static void TryMetric(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "Callers provide a bounded outcome.");
        try { BudgetMetrics.RecordScopeCreate(outcome); } catch (Exception) { }
    }
}
