// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Reserves and settles hierarchical child budgets over the budget authority.</summary>
/// <remarks>
/// <para>
/// A child's ceiling must fit strictly inside what its parent holds: turns and tool calls no larger, and a child ceiling
/// smaller than the parent's so delegation depth is bounded by budget as well as by profile. When a budget authority is
/// composed, the reservation is also recorded as a child scope beneath the parent's scope, keyed by the delegation's
/// idempotency identity, so a retried delegation receives the same scope instead of reserving twice. Without an authority
/// the ceilings are enforced arithmetically and the reservation carries no scope.
/// </para>
/// <para>
/// The scope records the ceiling as hard limits for the child. The engine-backed child runner passes that scope as
/// <c>AgentRunOptions.BudgetParentScopeId</c>, so the child run's own budget scope is created beneath it and the run is
/// bounded by the reserved ceiling as well as by the run's turn limit and the delegation deadline. Settlement compares known
/// usage to the ceiling and never counts unknown token usage as an overrun.
/// </para>
/// </remarks>
/// <param name="authority">The optional budget authority that holds child scopes.</param>
internal sealed class DefaultGoalBudgetManager(IBudgetAuthority? authority = null): IGoalBudgetManager
{
    private static readonly BudgetUnit _count = new("count");

    /// <inheritdoc/>
    public async ValueTask<GoalBudgetReserveResult> ReserveAsync(GoalBudgetReserveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var delegation = request.Delegation;
        var requested = delegation.Budget.Budget;
        var parent = request.ParentBudget;
        if (requested.MaximumTurns > parent.MaximumTurns
            || requested.MaximumToolCalls > parent.MaximumToolCalls
            || requested.MaximumChildren >= parent.MaximumChildren)
        {
            return Rejected("The requested child budget does not fit inside the parent's budget.");
        }

        if (authority is null)
        {
            return new GoalBudgetReserved(new GoalBudgetReservation(requested));
        }

        var identity = delegation.Authorization.Identity;
        var limits = ImmutableArray.CreateBuilder<BudgetLimit>();
        limits.Add(new BudgetLimit(BudgetDimensions.Turns, requested.MaximumTurns, _count, BudgetLimitKind.Hard));
        limits.Add(new BudgetLimit(BudgetDimensions.AttemptedToolCalls, requested.MaximumToolCalls, _count, BudgetLimitKind.Hard));
        if (requested.MaximumChildren > 0)
        {
            limits.Add(new BudgetLimit(BudgetDimensions.Delegations, requested.MaximumChildren, _count, BudgetLimitKind.Hard));
        }

        var result = await authority.CreateChildScopeAsync(
            new BudgetScopeRequest(
                request.ParentScopeId,
                new BudgetScopeAddress(identity.TenantId, identity.PrincipalId, delegation.TargetAgentId, sessionId: null, runId: null, operationId: null),
                limits.ToImmutable(),
                new IdempotencyKey($"agentkit.goals.budget:{delegation.ParentGoalId}:{delegation.IdempotencyKey.Value}")),
            cancellationToken).ConfigureAwait(false);
        return result is BudgetScopeCreated created
            ? new GoalBudgetReserved(new GoalBudgetReservation(requested, created.Scope.Id))
            : Rejected("The budget authority could not reserve the child budget.");
    }

    /// <inheritdoc/>
    public ValueTask<GoalBudgetSettlement> SettleAsync(GoalBudgetSettleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var budget = request.Reservation.Budget;
        var usage = request.Usage;
        return ValueTask.FromResult(new GoalBudgetSettlement(
            usage.Turns <= budget.MaximumTurns && usage.ToolCalls <= budget.MaximumToolCalls && usage.Children <= budget.MaximumChildren));
    }

    private static GoalBudgetRejected Rejected(string message) =>
        new(new DelegationRejection(DelegationRejectionKind.BudgetUnavailable, message));
}
