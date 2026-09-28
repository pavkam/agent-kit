// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Captures one bounded metric measurement observed through a <see cref="MeterListener"/>.</summary>
/// <param name="InstrumentName">The instrument that emitted the measurement.</param>
/// <param name="Tags">The shallow tag snapshot attached to the measurement.</param>
/// <param name="LongValue">The long measurement when applicable.</param>
/// <param name="DoubleValue">The double measurement when applicable.</param>
public sealed record MetricObservation(
    string InstrumentName,
    IReadOnlyDictionary<string, object?> Tags,
    long? LongValue,
    double? DoubleValue);
