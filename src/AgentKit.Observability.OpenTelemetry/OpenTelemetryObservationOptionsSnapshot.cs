// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Immutable OpenTelemetry exporter options captured for one keyed sink pair.</summary>
internal sealed record OpenTelemetryObservationOptionsSnapshot(
    ObservationExporterKey ExporterKey,
    ObservationExporterVersion ExporterVersion,
    OpenTelemetrySignalSet Signals,
    ObservationContentCapturePolicy ContentCapture,
    ObservationDeliveryPolicy Delivery,
    ObservationBounds Bounds);
