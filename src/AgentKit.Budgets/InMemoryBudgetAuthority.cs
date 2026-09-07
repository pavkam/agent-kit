// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>
/// The default, in-memory, process-singleton <see cref="IBudgetAuthority"/>.
/// </summary>
/// <remarks>
/// See <see cref="IBudgetAuthority"/> for the reduced-scope rationale shared
/// by this implementation: scope limits are supplied directly on
/// <see cref="BudgetScopeRequest"/> rather than resolved from a named,
/// keyed profile, and there is no separate ledger, policy catalog, or event
/// dispatcher yet. This authority is a thread-safe singleton; every scope it
/// creates is an owned, short-lived handle.
/// </remarks>
internal sealed class InMemoryBudgetAuthority: IBudgetAuthority
{
    private readonly Lock _gate = new();
    private readonly Dictionary<BudgetScopeId, InMemoryBudgetScope> _scopesById = [];
    private readonly Dictionary<IdempotencyKey, InMemoryBudgetScope> _scopesByKey = [];
    private readonly IBudgetDimensionCatalog _dimensions;
    private readonly IIdentifierGenerator<BudgetScopeId> _scopeIds;
    private readonly IIdentifierGenerator<BudgetReservationId> _reservationIds;
    private readonly TimeProvider _timeProvider;
    private readonly AgentBudgetOptionsSnapshot _options;

    /// <summary>Initializes a new instance of the <see cref="InMemoryBudgetAuthority"/> class.</summary>
    /// <param name="dimensions">Resolves the registered descriptor for a dimension referenced by a scope's limits.</param>
    /// <param name="scopeIds">Generates identities for created scopes.</param>
    /// <param name="reservationIds">Generates identities for reservations created against those scopes.</param>
    /// <param name="timeProvider">The clock used to timestamp and expire reservations.</param>
    /// <param name="options">The validated authority options.</param>
    /// <exception cref="ArgumentNullException">Any parameter is null.</exception>
    public InMemoryBudgetAuthority(
        IBudgetDimensionCatalog dimensions,
        IIdentifierGenerator<BudgetScopeId> scopeIds,
        IIdentifierGenerator<BudgetReservationId> reservationIds,
        TimeProvider timeProvider,
        AgentBudgetOptionsSnapshot options)
    {
        ArgumentNullException.ThrowIfNull(dimensions);
        ArgumentNullException.ThrowIfNull(scopeIds);
        ArgumentNullException.ThrowIfNull(reservationIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        _dimensions = dimensions;
        _scopeIds = scopeIds;
        _reservationIds = reservationIds;
        _timeProvider = timeProvider;
        _options = options;
    }

    /// <inheritdoc/>
    public ValueTask<BudgetScopeResult> CreateChildScopeAsync(
        BudgetScopeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_scopesByKey.TryGetValue(request.IdempotencyKey, out var existingScope))
            {
                return ValueTask.FromResult<BudgetScopeResult>(new BudgetScopeCreated(existingScope));
            }

            InMemoryBudgetScope? parent = null;
            if (request.ParentScopeId is { } parentId)
            {
                if (!_scopesById.TryGetValue(parentId, out parent))
                {
                    return ValueTask.FromResult<BudgetScopeResult>(
                        new BudgetScopeCreationFailed(
                            BudgetScopeCreationFailureKind.ParentNotFound,
                            $"No scope is registered with id '{parentId}'."));
                }
            }

            var depth = (parent?.Depth ?? -1) + 1;
            if (depth >= _options.MaximumScopeDepth)
            {
                return ValueTask.FromResult<BudgetScopeResult>(
                    new BudgetScopeCreationFailed(
                        BudgetScopeCreationFailureKind.MaximumDepthExceeded,
                        $"Creating this scope would reach depth {depth}, exceeding the configured maximum of " +
                            $"{_options.MaximumScopeDepth}."));
            }

            var invalidLimit = ValidateLimits(request.Limits, parent);
            if (invalidLimit is not null)
            {
                return ValueTask.FromResult<BudgetScopeResult>(invalidLimit);
            }

            var scope = new InMemoryBudgetScope(
                _scopeIds.Create(), request.Address, parent, request.Limits, _reservationIds, _timeProvider, _options);

            _scopesById[scope.Id] = scope;
            _scopesByKey[request.IdempotencyKey] = scope;

            return ValueTask.FromResult<BudgetScopeResult>(new BudgetScopeCreated(scope));
        }
    }

    private BudgetScopeCreationFailed? ValidateLimits(ImmutableArray<BudgetLimit> limits, InMemoryBudgetScope? parent)
    {
        foreach (var limit in limits)
        {
            if (!_dimensions.TryGet(limit.Dimension, out var descriptor))
            {
                return new BudgetScopeCreationFailed(
                    BudgetScopeCreationFailureKind.InvalidLimit,
                    $"Dimension '{limit.Dimension}' has no registered descriptor.");
            }

            if (!descriptor.AllowedUnits.Contains(limit.Unit))
            {
                return new BudgetScopeCreationFailed(
                    BudgetScopeCreationFailureKind.InvalidLimit,
                    $"Unit '{limit.Unit}' is not a legal unit for dimension '{limit.Dimension}'.");
            }

            var ancestor = parent;
            while (ancestor is not null)
            {
                if (ancestor.Limits.TryGetValue(limit.Dimension, out var ancestorLimit)
                    && ancestorLimit.Kind == BudgetLimitKind.Hard
                    && limit.Value > ancestorLimit.Value)
                {
                    return new BudgetScopeCreationFailed(
                        BudgetScopeCreationFailureKind.LimitWiderThanAncestor,
                        $"Limit {limit.Value} for dimension '{limit.Dimension}' exceeds ancestor hard limit " +
                            $"{ancestorLimit.Value}.");
                }

                ancestor = ancestor.Parent;
            }
        }

        return null;
    }
}
