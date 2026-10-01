// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Mutable binding values for one keyed OpenTelemetry observation exporter.</summary>
/// <remarks>
/// <para>
/// The values are validated when <c>AddOpenTelemetryObservability</c> runs and copied into one immutable
/// <see cref="OpenTelemetryObservationOptionsSnapshot"/>; later mutation of this instance has no effect. Changing an
/// exporter's behavior means publishing a new <see cref="ExporterVersion"/> under a new registration.
/// </para>
/// <para>
/// Content capture is disabled by default. Enabling it needs a replacement <see cref="IObservationRedactor"/> that is not the
/// omission-only default, a content classification the policy allows, and a positive byte bound.
/// </para>
/// </remarks>
public sealed class OpenTelemetryObservationOptions
{
    /// <summary>Gets or sets the monotonic exporter version copied into the immutable snapshot at registration.</summary>
    /// <value>A positive generation number. The default is 1.</value>
    public long ExporterVersion { get; set; } = 1;

    /// <summary>Gets or sets the enabled OpenTelemetry signal families.</summary>
    /// <value>A non-empty combination of defined flags. The default is activities and metrics.</value>
    public OpenTelemetrySignalSet Signals { get; set; } = OpenTelemetrySignalSet.Activities | OpenTelemetrySignalSet.Metrics;

    /// <summary>Gets or sets whether classified payload capture is enabled.</summary>
    /// <value>A non-null policy. The default disables capture.</value>
    public ObservationContentCapturePolicy ContentCapture { get; set; } = new();

    /// <summary>Gets or sets the classification this exporter declares for the content it extracts from run events.</summary>
    /// <value>A defined classification. The default is <see cref="DataClassification.Confidential"/>.</value>
    public DataClassification ContentClassification { get; set; } = DataClassification.Confidential;

    /// <summary>Gets or sets the classifications permitted to survive redaction.</summary>
    /// <value>
    /// A non-null set. Content whose classification is outside it is omitted without invoking the redactor.
    /// The default is <see cref="DataClassification.Public"/> and <see cref="DataClassification.Internal"/>.
    /// </value>
    public ImmutableHashSet<DataClassification> AllowedClassifications { get; set; } =
        [DataClassification.Public, DataClassification.Internal];

    /// <summary>Gets or sets the delivery and shutdown policy for this exporter's sinks.</summary>
    /// <value>
    /// A non-null policy. A required policy makes the run-event sink required and, when the audit signal is enabled,
    /// requires <see cref="AuditExporterProvidesDurableAcceptance"/>.
    /// </value>
    public ObservationDeliveryPolicy Delivery { get; set; } = new();

    /// <summary>Gets or sets the maximum capture bounds enforced before and after redaction.</summary>
    /// <value>A non-null bound. The default is 16,384 bytes per field.</value>
    public ObservationBounds Bounds { get; set; } = new(16_384);

    /// <summary>Gets or sets whether the host's <see cref="IOpenTelemetryAuditExporter"/> proves durable acceptance.</summary>
    /// <value><see langword="false"/> by default. A required audit delivery is rejected at registration unless this is <see langword="true"/>.</value>
    public bool AuditExporterProvidesDurableAcceptance { get; set; }
}
