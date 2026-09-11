// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains an immutable current usage projection for one run with attributable, revisioned contributions.</summary>
/// <remarks>This value never mutates a previously returned result and is not a durable usage ledger or budget authority. Session storage retains the append-only history and validates authorized corrections. Producers bound retained entries and measurements before constructing a result.</remarks>
public sealed record RunUsage
{
    /// <summary>Captures a validated current projection, including restored noninitial revisions.</summary>
    /// <param name="runId">The nondefault owning run.</param>
    /// <param name="entries">Initialized current contributions unique by entry identity and belonging to this run.</param>
    /// <exception cref="ArgumentOutOfRangeException">The run identity is default.</exception>
    /// <exception cref="ArgumentNullException">An entry is null.</exception>
    /// <exception cref="ArgumentException">Entries are uninitialized, duplicate an identity, or address another run.</exception>
    public RunUsage(RunId runId, ImmutableArray<UsageAccountingEntry> entries)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentException.ThrowIfDefault(entries);
        HashSet<UsageEntryId> keys = [];
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entries));
            ArgumentException.ThrowIfNotEqual(entry.RunId, runId, nameof(entries));
            ArgumentException.ThrowIfNotEqual(keys.Add(entry.Id), true, nameof(entries));
        }
        RunId = runId; Entries = entries;
    }

    /// <summary>Gets the run owning every contribution.</summary>
    /// <value>A nondefault identity that is preserved across immutable replacements.</value>
    public RunId RunId { get; }
    /// <summary>Gets the latest complete revision of each charged invocation.</summary>
    /// <value>An initialized immutable array in first-seen order, including failed and retried attempts.</value>
    public ImmutableArray<UsageAccountingEntry> Entries { get; }

    /// <summary>Returns a projection with one initial contribution or exact next replacement revision.</summary>
    /// <param name="entry">The nonnull full contribution addressing this run.</param>
    /// <returns>This same instance for an equivalent current-revision replay; otherwise a new snapshot preserving prior snapshots and entry order.</returns>
    /// <exception cref="ArgumentNullException">The entry is null.</exception>
    /// <exception cref="ArgumentException">The entry addresses another run, skips its initial or next revision, changes attribution, conflicts with a current revision, or regresses provider report lifecycle.</exception>
    /// <remarks>Older revisions require ledger-level deduplication and are rejected here. A correction replaces the entire prior contribution, permitting lower totals without double-counting cumulative reports. This method neither authorizes nor persists the correction.</remarks>
    public RunUsage Apply(UsageAccountingEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNotEqual(entry.RunId, RunId, nameof(entry));
        for (var index = 0; index < Entries.Length; index++)
        {
            var current = Entries[index];
            if (current.Id != entry.Id) { continue; }
            if (current == entry) { return this; }
            ArgumentException.ThrowIfNotEqual(entry.PreviousRevision, current.Revision, nameof(entry));
            ArgumentException.ThrowIfNotEqual(entry.OperationId, current.OperationId, nameof(entry));
            ArgumentException.ThrowIfNotEqual(entry.Model, current.Model, nameof(entry));
            if (current.ProviderUsage is { } prior && entry.ProviderUsage is { } next)
            {
                ArgumentException.ThrowIfNotEqual(prior.ReportState != ModelUsageReportState.Final || next.ReportState == ModelUsageReportState.Final, true, nameof(entry));
                ArgumentException.ThrowIfNotEqual(prior.ReportState == ModelUsageReportState.NotReported || next.ReportState != ModelUsageReportState.NotReported, true, nameof(entry));
            }
            return new RunUsage(RunId, Entries.SetItem(index, entry));
        }
        ArgumentException.ThrowIfNotEqual(entry.Revision.Value, 1L, nameof(entry));
        return new RunUsage(RunId, Entries.Add(entry));
    }

    /// <summary>Aggregates current observations using an explicitly supplied dimension descriptor and legal unit.</summary>
    /// <param name="descriptor">The nonnull validated captured descriptor defining aggregation and legal units.</param>
    /// <param name="unit">The nondefault legal unit to select.</param>
    /// <returns>Exact known evidence and nullable complete amount, preserving individual measurement quality and pricing provenance.</returns>
    /// <exception cref="ArgumentNullException">The descriptor is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A dimension or unit is default, or aggregation is undefined.</exception>
    /// <exception cref="ArgumentException">Allowed units are uninitialized, empty, duplicate, or do not contain the selected unit.</exception>
    /// <remarks>No unit conversion occurs. Concurrent gauges sum the latest explicit observations; proven completion must publish a zero replacement and is never inferred from a returned snapshot. An omitted dimension contributes unknown unless explicitly not applicable. A dimension reported solely in other units does not contribute to this unit. Catalog selection and shared-limit enforcement remain outside this value.</remarks>
    public RunUsageAggregate GetAggregate(BudgetDimensionDescriptor descriptor, BudgetUnit unit)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentOutOfRangeException.ThrowIfEqual(descriptor.Dimension, default, nameof(descriptor));
        ArgumentOutOfRangeException.ThrowIfUndefined(descriptor.Aggregation, nameof(descriptor));
        ArgumentOutOfRangeException.ThrowIfEqual(unit, default);
        ArgumentException.ThrowIfDefaultOrEmpty(descriptor.AllowedUnits, nameof(descriptor));
        HashSet<BudgetUnit> units = [];
        foreach (var allowed in descriptor.AllowedUnits)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(allowed, default, nameof(descriptor));
            ArgumentException.ThrowIfNotEqual(units.Add(allowed), true, nameof(descriptor));
        }
        ArgumentException.ThrowIfNotEqual(units.Contains(unit), true, nameof(unit));
        var measurements = Entries.Select(entry => entry.Measurements.FirstOrDefault(item => item.Dimension == descriptor.Dimension && item.Unit == unit)
            ?? new UsageMeasurement(descriptor.Dimension, unit, null,
                entry.Measurements.Any(item => item.Dimension == descriptor.Dimension)
                    ? UsageMeasurementQuality.NotApplicable : UsageMeasurementQuality.Unknown)).ToImmutableArray();
        return new RunUsageAggregate(descriptor.Dimension, unit, descriptor.Aggregation, measurements);
    }

    /// <summary>Compares run identity and ordered current usage evidence structurally.</summary>
    /// <param name="other">The candidate projection, or null.</param>
    /// <returns>True only for equivalent run identity and ordered entry evidence.</returns>
    public bool Equals(RunUsage? other) => other is not null && RunId == other.RunId && Entries.SequenceEqual(other.Entries);
    /// <summary>Hashes the run and ordered contribution evidence consistently with equality.</summary>
    /// <returns>A structural hash over the immutable snapshot.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RunId);
        foreach (var entry in Entries) { hash.Add(entry); }
        return hash.ToHashCode();
    }
}
