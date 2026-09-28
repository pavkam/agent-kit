// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Mutable binding values for one keyed OpenTelemetry observation exporter.</summary>
public sealed class OpenTelemetryObservationOptions
{
    /// <summary>Gets or sets the monotonic exporter version copied into the immutable snapshot at registration.</summary>
    public long ExporterVersion { get; set; } = 1;

    /// <summary>Gets or sets the enabled OpenTelemetry signal families.</summary>
    public OpenTelemetrySignalSet Signals { get; set; } = OpenTelemetrySignalSet.Activities | OpenTelemetrySignalSet.Metrics;

    /// <summary>Gets or sets whether classified payload capture is enabled.</summary>
    public ObservationContentCapturePolicy ContentCapture { get; set; } = new();

    /// <summary>Gets or sets the delivery and shutdown policy for required sinks.</summary>
    public ObservationDeliveryPolicy Delivery { get; set; } = new();

    /// <summary>Gets or sets the maximum capture bounds enforced before export.</summary>
    public ObservationBounds Bounds { get; set; } = new(16_384);
}
