// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one immutable options snapshot generation for a keyed observation exporter.</summary>
/// <remarks>Changing exporter configuration requires publishing a new version; sinks finish in-flight work with the snapshot they captured at activation.</remarks>
public readonly record struct ObservationExporterVersion
{
    /// <summary>Initializes a validated exporter version.</summary>
    /// <param name="value">The positive monotonic version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ObservationExporterVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(value));
        Value = value;
    }

    /// <summary>Gets the positive version number.</summary>
    public long Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
