// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The single, shared, thread-safe authority owning the hierarchical budget
/// ledger for one engine process.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately reduced stand-in for the fuller
/// <c>IBudgetAuthority</c> described by the budgets architecture, which
/// additionally composes a separate <c>IBudgetLedger</c>, keyed
/// <c>IBudgetProfileCatalog</c>/<c>IBudgetPolicyCatalog</c> selection, and an
/// <c>IBudgetEventDispatcher</c> for soft-limit notification. Until those
/// depend-on subsystems exist — profile/policy keying needs the
/// not-yet-implemented <c>ComponentKey&lt;T&gt;</c> selection machinery, and
/// event dispatch needs a documented observability sink — this authority
/// performs ledger accounting directly and a scope's limits are supplied
/// explicitly on <see cref="BudgetScopeRequest"/> rather than resolved from
/// a named profile.
/// </para>
/// <para>
/// The authority is a process singleton. Host, tenant, principal, agent,
/// session, run, and operation ownership are addresses inside its one
/// shared ledger, not separate authority instances or DI scopes. Every
/// returned <see cref="IBudgetScope"/> is an owned, short-lived handle; it
/// is never captured by a singleton consumer.
/// </para>
/// </remarks>
public interface IBudgetAuthority
{
    /// <summary>Creates a new budget scope, or idempotently returns an existing one created by an identical prior request.</summary>
    /// <param name="request">The scope creation request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task producing the terminal outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<BudgetScopeResult> CreateChildScopeAsync(
        BudgetScopeRequest request, CancellationToken cancellationToken = default);
}
