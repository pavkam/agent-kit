// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the outcome of planning one mutation against in-memory goal state, before anything is persisted.</summary>
/// <remarks>Adapters that persist must write <see cref="Record"/> durably and only then call <see cref="GoalStoreState.Commit"/>, so memory never gets ahead of disk.</remarks>
internal sealed class GoalPlan
{
    private GoalPlan(GoalReductionKind kind, TenantId tenant, string? createKey, GoalRecord? record, GoalStoreFailure? failure)
    {
        Kind = kind;
        Tenant = tenant;
        CreateKey = createKey;
        Record = record;
        Failure = failure;
    }

    /// <summary>Gets the outcome class.</summary>
    internal GoalReductionKind Kind { get; }

    /// <summary>Gets the tenant partition the record belongs to.</summary>
    internal TenantId Tenant { get; }

    /// <summary>Gets the creation idempotency key to index, or <see langword="null"/> for a transition.</summary>
    internal string? CreateKey { get; }

    /// <summary>Gets the aggregate to persist (applied) or return (replayed), or <see langword="null"/> when rejected.</summary>
    internal GoalRecord? Record { get; }

    /// <summary>Gets the typed refusal, or <see langword="null"/> unless rejected.</summary>
    internal GoalStoreFailure? Failure { get; }

    /// <summary>Creates an applied plan.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="createKey">The creation key to index, or <see langword="null"/>.</param>
    /// <param name="record">The aggregate to persist.</param>
    /// <returns>The plan.</returns>
    internal static GoalPlan Applied(TenantId tenant, string? createKey, GoalRecord record) => new(GoalReductionKind.Applied, tenant, createKey, record, null);

    /// <summary>Creates a replayed plan.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="record">The stored aggregate.</param>
    /// <returns>The plan.</returns>
    internal static GoalPlan Replayed(TenantId tenant, GoalRecord record) => new(GoalReductionKind.Replayed, tenant, null, record, null);

    /// <summary>Creates a rejected plan.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="kind">The failure class.</param>
    /// <param name="message">The content-safe explanation.</param>
    /// <returns>The plan.</returns>
    internal static GoalPlan Rejected(TenantId tenant, GoalStoreFailureKind kind, string message) =>
        new(GoalReductionKind.Rejected, tenant, null, null, new GoalStoreFailure(kind, message));

    /// <summary>Creates a rejected plan from an existing failure.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="failure">The typed refusal.</param>
    /// <returns>The plan.</returns>
    internal static GoalPlan Rejected(TenantId tenant, GoalStoreFailure failure) =>
        new(GoalReductionKind.Rejected, tenant, null, null, failure);
}
