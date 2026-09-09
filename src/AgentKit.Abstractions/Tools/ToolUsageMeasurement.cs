// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one dimension/unit tool-usage observation without settling a budget.</summary>
public sealed record ToolUsageMeasurement
{
    /// <summary>Initializes one usage measurement.</summary>
    /// <param name="dimension">Usage dimension.</param><param name="unit">Usage unit.</param><param name="amount">Exact known amount or null.</param><param name="quality">Defined observation quality.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimension"/> or <paramref name="unit"/> is default, or <paramref name="quality"/> is undefined.</exception>
    /// <exception cref="ArgumentException">Amount presence conflicts with <paramref name="quality"/>.</exception>
    public ToolUsageMeasurement(BudgetDimension dimension, BudgetUnit unit, BudgetQuantity? amount, ToolUsageMeasurementQuality quality)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(dimension, default);
        ArgumentOutOfRangeException.ThrowIfEqual(unit, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(quality);
        ArgumentException.ThrowIfNotEqual(quality is ToolUsageMeasurementQuality.Measured or ToolUsageMeasurementQuality.Estimated, amount.HasValue, nameof(amount));
        Dimension = dimension; Unit = unit; Amount = amount; Quality = quality;
    }
    /// <summary>Gets the dimension.</summary>
    /// <value>The reported dimension.</value>
    public BudgetDimension Dimension { get; }
    /// <summary>Gets the unit.</summary>
    /// <value>The reported unit.</value>
    public BudgetUnit Unit { get; }
    /// <summary>Gets the exact amount when known.</summary>
    /// <value>Null for unknown or not-applicable quality.</value>
    public BudgetQuantity? Amount { get; }
    /// <summary>Gets the observation quality.</summary>
    /// <value>A defined quality.</value>
    public ToolUsageMeasurementQuality Quality { get; }
}
