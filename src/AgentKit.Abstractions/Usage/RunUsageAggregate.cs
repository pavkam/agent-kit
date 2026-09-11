// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes an exact aggregate without hiding missing observations or mixed measurement provenance.</summary>
/// <remarks>The contributing measurements retain their individual qualities and pricing references. KnownAmount is only the known portion when a contribution is unknown; it must not be treated as a complete total.</remarks>
public sealed record RunUsageAggregate
{
    /// <summary>Combines already selected observations using explicit declared aggregation.</summary>
    /// <param name="dimension">The nondefault selected dimension.</param>
    /// <param name="unit">The nondefault selected unit.</param>
    /// <param name="aggregation">The defined aggregation of current observations.</param>
    /// <param name="measurements">Initialized nonnull contributions all matching the dimension and unit.</param>
    /// <exception cref="ArgumentOutOfRangeException">Dimension or unit is default, or aggregation is undefined.</exception>
    /// <exception cref="ArgumentNullException">A contribution is null.</exception>
    /// <exception cref="ArgumentException">Contributions are uninitialized or contain a mismatched dimension or unit.</exception>
    internal RunUsageAggregate(BudgetDimension dimension, BudgetUnit unit, BudgetAggregationKind aggregation, ImmutableArray<UsageMeasurement> measurements)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(dimension, default);
        ArgumentOutOfRangeException.ThrowIfEqual(unit, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(aggregation);
        ArgumentException.ThrowIfDefault(measurements);
        foreach (var measurement in measurements)
        {
            ArgumentNullException.ThrowIfNull(measurement, nameof(measurements));
            ArgumentException.ThrowIfNotEqual(measurement.Dimension, dimension, nameof(measurements));
            ArgumentException.ThrowIfNotEqual(measurement.Unit, unit, nameof(measurements));
        }
        BudgetQuantity known = default;
        foreach (var measurement in measurements)
        {
            if (measurement.Amount is not { } amount) { continue; }
            known = aggregation == BudgetAggregationKind.Maximum ? (amount > known ? amount : known) : known.Add(amount);
        }
        Dimension = dimension; Unit = unit; Aggregation = aggregation; Measurements = measurements; KnownAmount = known;
    }

    /// <summary>Gets the dimension selected for aggregation.</summary>
    /// <value>A nondefault dimension; token categories remain independent.</value>
    public BudgetDimension Dimension { get; }
    /// <summary>Gets the single selected unit.</summary>
    /// <value>A nondefault unit; currencies are never mixed or converted.</value>
    public BudgetUnit Unit { get; }
    /// <summary>Gets the declared rule applied to current observations.</summary>
    /// <value>Sum, duration, and concurrent gauge sum observations; maximum selects the largest known observation.</value>
    public BudgetAggregationKind Aggregation { get; }
    /// <summary>Gets the ordered contributions, retaining each source's quality and pricing evidence.</summary>
    /// <value>An immutable array in entry order; empty means there is no applicable evidence.</value>
    public ImmutableArray<UsageMeasurement> Measurements { get; }
    /// <summary>Gets the exact aggregate of only the known observations.</summary>
    /// <value>Exact zero when no observation is known; consult Amount before treating this as complete.</value>
    public BudgetQuantity KnownAmount { get; }
    /// <summary>Gets a complete amount only when every contributor reports a known value.</summary>
    /// <value>Null when there are no applicable contributions or any contribution is unknown.</value>
    public BudgetQuantity? Amount => !Measurements.Any(static item => item.Quality != UsageMeasurementQuality.NotApplicable)
        || Measurements.Any(static item => item.Quality == UsageMeasurementQuality.Unknown) ? null : KnownAmount;
    /// <summary>Gets whether a contributing amount was estimated.</summary>
    /// <value>True when any retained contribution has estimated quality, even when other contributions are measured or unknown.</value>
    public bool HasEstimates => Measurements.Any(static item => item.Quality == UsageMeasurementQuality.Estimated);

    /// <summary>Compares aggregate evidence and its ordered contributions structurally.</summary>
    /// <param name="other">The candidate aggregate, or null.</param>
    /// <returns>True for equivalent dimensions, units, aggregation and contribution evidence.</returns>
    public bool Equals(RunUsageAggregate? other) => other is not null && Dimension == other.Dimension
        && Unit == other.Unit && Aggregation == other.Aggregation && Measurements.SequenceEqual(other.Measurements);
    /// <summary>Hashes aggregate evidence consistently with structural equality.</summary>
    /// <returns>A hash over the declared aggregation and ordered measurements.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Dimension); hash.Add(Unit); hash.Add(Aggregation);
        foreach (var measurement in Measurements) { hash.Add(measurement); }
        return hash.ToHashCode();
    }
}
