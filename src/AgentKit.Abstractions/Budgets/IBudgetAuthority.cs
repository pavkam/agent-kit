// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The single, shared, thread-safe runtime boundary for hierarchical budgets in
/// one engine composition.
/// </summary>
/// <remarks>
/// <para>
/// The first-party runtime delegates authoritative persistence and accounting
/// to an explicitly composed <see cref="IBudgetLedger"/>. Named budget-profile
/// and policy selection and soft-limit event dispatch remain pending; until
/// those integrations exist, callers supply a scope's limits directly on
/// <see cref="BudgetScopeRequest"/>.
/// </para>
/// <para>
/// The authority is a process singleton. Host, tenant, principal, agent,
/// session, run, and operation ownership are addresses inside its one
/// selected ledger, not separate authority instances or DI scopes. Every
/// returned <see cref="IBudgetScope"/> is an owned, short-lived handle; it
/// is never captured by a singleton consumer. Run-profile compilation will
/// later expose an <see cref="IRunBudget"/> from explicit run-scope evidence;
/// ordinary scope creation never infers or fabricates that specialization.
/// </para>
/// </remarks>
public interface IBudgetAuthority
{
    /// <summary>Creates a new budget scope, or idempotently returns an existing one created by an identical prior request.</summary>
    /// <param name="request">The scope creation request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="BudgetLedgerPersistenceUnavailableException">The selected ledger cannot confirm creation; retry the exact immutable request when acknowledgement may be unknown.</exception>
    public ValueTask<BudgetScopeResult> CreateChildScopeAsync(
        BudgetScopeRequest request, CancellationToken cancellationToken = default);
}
