// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one exact usage observation with explicit unit and measurement provenance.</summary>
/// <remarks>Input, output, cached-read, cached-write, and reasoning tokens use separate dimensions. No total-token formula or currency conversion is inferred. Values are observations and never settle or release a budget reservation.</remarks>
public sealed record UsageMeasurement
{
    /// <summary>Validates one known or explicitly unknown measurement before capturing it.</summary>
    /// <param name="dimension">The nondefault dimension.</param>
    /// <param name="unit">The nondefault unit; cost uses an explicit currency.</param>
    /// <param name="amount">The exact nonnegative amount, or null for unknown or not-applicable quality.</param>
    /// <param name="quality">The defined measurement provenance.</param>
    /// <param name="pricing">Pricing provenance, required for estimated cost and permitted only for cost.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension or unit is default, or quality is undefined.</exception>
    /// <exception cref="ArgumentException">Amount presence conflicts with quality, or pricing is present for a non-cost dimension.</exception>
    /// <exception cref="ArgumentNullException">Estimated cost has no pricing provenance.</exception>
    public UsageMeasurement(BudgetDimension dimension, BudgetUnit unit, BudgetQuantity? amount, UsageMeasurementQuality quality, UsagePricingReference? pricing = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(dimension, default);
        ArgumentOutOfRangeException.ThrowIfEqual(unit, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(quality);
        ArgumentException.ThrowIfNotEqual(amount.HasValue, quality is UsageMeasurementQuality.Measured or UsageMeasurementQuality.ProviderReported or UsageMeasurementQuality.Estimated, nameof(amount));
        if (dimension == BudgetDimensions.Cost && quality == UsageMeasurementQuality.Estimated) { ArgumentNullException.ThrowIfNull(pricing); }
        if (pricing is not null) { ArgumentException.ThrowIfNotEqual(dimension, BudgetDimensions.Cost, nameof(pricing)); }
        Dimension = dimension; Unit = unit; Amount = amount; Quality = quality; Pricing = pricing;
    }

    /// <summary>Gets the separately accounted dimension.</summary>
    /// <value>A nondefault dimension whose aggregation must be supplied explicitly.</value>
    public BudgetDimension Dimension { get; }
    /// <summary>Gets the unit without implicit normalization or conversion.</summary>
    /// <value>A nondefault unit; different currency units never combine.</value>
    public BudgetUnit Unit { get; }
    /// <summary>Gets the exact known amount without decimal overflow or rounding.</summary>
    /// <value>Null for unknown or not-applicable quality; explicit zero is a known amount.</value>
    public BudgetQuantity? Amount { get; }
    /// <summary>Gets the measurement's evidential source independently of its amount.</summary>
    /// <value>A defined quality retained through aggregation and replacement.</value>
    public UsageMeasurementQuality Quality { get; }
    /// <summary>Gets immutable cost-estimation provenance when supplied.</summary>
    /// <value>Required for estimated cost, null for every non-cost dimension.</value>
    public UsagePricingReference? Pricing { get; }
}
