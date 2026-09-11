// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains one immutable revision of a charged invocation's complete usage observations.</summary>
/// <remarks>Each revision replaces the entire prior measurement set. Failed, retried, cancelled and synthetic attempts retain distinct entries. This value is evidence, not proof of persistence, settlement, or effect completion.</remarks>
public sealed record UsageAccountingEntry
{
    /// <summary>Captures one validated complete contribution revision.</summary>
    /// <param name="id">The nondefault identity retained across reports and corrections.</param>
    /// <param name="runId">The nondefault owning run.</param>
    /// <param name="operationId">The nondefault causal operation, which may own several distinct attempts.</param>
    /// <param name="revision">The positive revision; initial entries use one.</param>
    /// <param name="previousRevision">Null for revision one; otherwise the immediately preceding positive revision.</param>
    /// <param name="measurements">Initialized nonnull measurements unique by dimension and unit.</param>
    /// <param name="model">Model attribution, or null for non-model usage.</param>
    /// <param name="providerUsage">The original provider report, required exactly when model attribution is present. Absence of reporting uses ModelUsage.NotReported.</param>
    /// <param name="extensions">Nonnull immutable additional accounting evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity or revision is default.</exception>
    /// <exception cref="ArgumentNullException">The measurements contain null or extensions are null.</exception>
    /// <exception cref="ArgumentException">Measurements are uninitialized or duplicate, revision linkage is invalid, or model/report presence differs.</exception>
    public UsageAccountingEntry(UsageEntryId id, RunId runId, OperationId operationId, UsageAccountingRevision revision,
        UsageAccountingRevision? previousRevision, ImmutableArray<UsageMeasurement> measurements,
        ModelUsageAttribution? model, ModelUsage? providerUsage, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(runId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(revision, default);
        if (previousRevision is { } previous) { ArgumentOutOfRangeException.ThrowIfEqual(previous, default, nameof(previousRevision)); }
        ArgumentException.ThrowIfNotEqual(previousRevision?.Value ?? 0, revision.Value - 1, nameof(previousRevision));
        ArgumentException.ThrowIfDefault(measurements);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentException.ThrowIfNotEqual(model is null, providerUsage is null, nameof(providerUsage));
        HashSet<(BudgetDimension, BudgetUnit)> keys = [];
        foreach (var measurement in measurements)
        {
            ArgumentNullException.ThrowIfNull(measurement, nameof(measurements));
            ArgumentException.ThrowIfNotEqual(keys.Add((measurement.Dimension, measurement.Unit)), true, nameof(measurements));
        }
        Id = id; RunId = runId; OperationId = operationId; Revision = revision; PreviousRevision = previousRevision;
        Measurements = measurements; Model = model; ProviderUsage = providerUsage; Extensions = extensions;
    }

    /// <summary>Gets the identity of the original charged invocation.</summary>
    /// <value>A nondefault identity that stays fixed during replacement.</value>
    public UsageEntryId Id { get; }
    /// <summary>Gets the run to which this contribution belongs.</summary>
    /// <value>A nondefault run identity; after-run work cannot be relabelled as active run usage.</value>
    public RunId RunId { get; }
    /// <summary>Gets the causal operation without conflating its retries.</summary>
    /// <value>A nondefault operation identity; entry identity distinguishes charged attempts.</value>
    public OperationId OperationId { get; }
    /// <summary>Gets the version of the complete replacement observation.</summary>
    /// <value>A positive revision independent of ledger sequence.</value>
    public UsageAccountingRevision Revision { get; }
    /// <summary>Gets the exact revision replaced by this observation.</summary>
    /// <value>Null at revision one; otherwise one less than the current revision.</value>
    public UsageAccountingRevision? PreviousRevision { get; }
    /// <summary>Gets the complete ordered measurement set for this revision.</summary>
    /// <value>An initialized immutable array; an empty set claims no known usage, not zero cost.</value>
    public ImmutableArray<UsageMeasurement> Measurements { get; }
    /// <summary>Gets the captured model route and request when this is a model attempt.</summary>
    /// <value>Nonnull exactly when a provider usage report is retained.</value>
    public ModelUsageAttribution? Model { get; }
    /// <summary>Gets the original provider report including provider-native extension counters.</summary>
    /// <value>Null for non-model usage; an explicit NotReported value preserves unknown reporting.</value>
    public ModelUsage? ProviderUsage { get; }
    /// <summary>Gets immutable additional provenance for this revision.</summary>
    /// <value>A nonnull extension bag; producers retain adjustment evidence here without exposing secrets.</value>
    public ExtensionData Extensions { get; }

    /// <summary>Compares complete contribution evidence, including ordered measurements.</summary>
    /// <param name="other">The candidate entry, or null.</param>
    /// <returns>True only for structurally equivalent revision evidence.</returns>
    public bool Equals(UsageAccountingEntry? other) => other is not null
        && Id == other.Id && RunId == other.RunId && OperationId == other.OperationId
        && Revision == other.Revision && PreviousRevision == other.PreviousRevision
        && Measurements.SequenceEqual(other.Measurements) && Model == other.Model
        && ProviderUsage == other.ProviderUsage && Extensions == other.Extensions;

    /// <summary>Hashes structural contribution evidence consistently with equality.</summary>
    /// <returns>A hash over correlation, revision and retained usage evidence.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id); hash.Add(RunId); hash.Add(OperationId); hash.Add(Revision); hash.Add(PreviousRevision);
        foreach (var measurement in Measurements) { hash.Add(measurement); }
        hash.Add(Model); hash.Add(ProviderUsage); hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
