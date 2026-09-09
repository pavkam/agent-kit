// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Stores immutable, unique tool-usage measurements.</summary>
public sealed record ToolUsage
{
    /// <summary>Initializes a usage report.</summary>
    /// <param name="measurements">Initialized measurements unique by dimension/unit.</param>
    /// <param name="extensions">Compatible immutable evidence.</param>
    /// <exception cref="ArgumentException"><paramref name="measurements"/> is default or duplicates a dimension/unit pair.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="measurements"/> contains null or <paramref name="extensions"/> is null.</exception>
    public ToolUsage(ImmutableArray<ToolUsageMeasurement> measurements, ExtensionData extensions)
    {
        ArgumentException.ThrowIfDefault(measurements);
        ArgumentNullException.ThrowIfNull(extensions);
        var keys = new HashSet<(BudgetDimension, BudgetUnit)>();
        foreach (var measurement in measurements)
        {
            ArgumentNullException.ThrowIfNull(measurement, nameof(measurements));
            ArgumentException.ThrowIfNotEqual(keys.Add((measurement.Dimension, measurement.Unit)), true, nameof(measurements));
        }
        Measurements = measurements; Extensions = extensions;
    }
    /// <summary>Gets ordered unique measurements.</summary>
    /// <value>An initialized immutable array.</value>
    public ImmutableArray<ToolUsageMeasurement> Measurements { get; }
    /// <summary>Gets compatible extensions.</summary>
    /// <value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }

    /// <summary>Determines structural usage-report equality.</summary>
    /// <param name="other">The report to compare, or null.</param>
    /// <returns>True when ordered measurements and extension evidence are equal.</returns>
    public bool Equals(ToolUsage? other) =>
        other is not null && Measurements.SequenceEqual(other.Measurements) && Extensions == other.Extensions;

    /// <summary>Returns a hash compatible with structural usage-report equality.</summary>
    /// <returns>A hash over ordered measurements and extension evidence.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var measurement in Measurements)
        {
            hash.Add(measurement);
        }

        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
