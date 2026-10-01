// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the complete persisted form of one goal snapshot in a durable adapter: tenant partition, creation key, aggregate, and delegation.</summary>
/// <param name="Tenant">The tenant partition text.</param>
/// <param name="CreateKey">The creation idempotency key to index, present only on the snapshot that created the goal.</param>
/// <param name="Record">The aggregate without its delegation.</param>
/// <param name="Delegation">The captured delegation, or <see langword="null"/> for a goal that was not delegated.</param>
internal sealed record PersistedGoalDocument(
    string Tenant,
    string? CreateKey,
    GoalRecordDocument Record,
    GoalDelegationDocument? Delegation)
{
    /// <summary>Captures one snapshot for persistence.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="createKey">The creation key to index, or <see langword="null"/>.</param>
    /// <param name="record">The aggregate to persist.</param>
    /// <returns>The document.</returns>
    internal static PersistedGoalDocument FromDomain(TenantId tenant, string? createKey, GoalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(
            tenant.Value,
            createKey,
            GoalRecordDocument.FromDomain(record),
            record.Delegation is null ? null : GoalDelegationDocument.FromDomain(record.Delegation));
    }

    /// <summary>Restores the tenant and aggregate, re-running every validation.</summary>
    /// <returns>The tenant, creation key, and aggregate.</returns>
    internal (TenantId Tenant, string? CreateKey, GoalRecord Record) ToDomain()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Tenant);
        ArgumentNullException.ThrowIfNull(Record);
        return (new TenantId(Tenant), CreateKey, Record.ToDomain(Delegation?.ToDomain()));
    }
}
