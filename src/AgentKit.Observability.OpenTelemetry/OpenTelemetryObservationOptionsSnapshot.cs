// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Immutable OpenTelemetry exporter options captured for one keyed sink pair.</summary>
/// <remarks>
/// Sinks receive the snapshot of exactly their own exporter key through a factory closure; they never inject an unkeyed
/// options object. In-flight deliveries finish with the snapshot they were created with. Equality is value-based,
/// including the allowed-classification set, so an identical repeated registration is recognized as idempotent.
/// </remarks>
/// <param name="ExporterKey">The stable exporter identity.</param>
/// <param name="ExporterVersion">The options generation.</param>
/// <param name="Signals">The enabled signal families.</param>
/// <param name="ContentCapture">Whether content capture is enabled.</param>
/// <param name="ContentClassification">The classification declared for extracted content.</param>
/// <param name="AllowedClassifications">The classifications permitted to survive redaction.</param>
/// <param name="Delivery">The delivery and flush policy.</param>
/// <param name="Bounds">The per-field capture bounds.</param>
/// <param name="AuditExporterProvidesDurableAcceptance">Whether the audit exporter proves durable acceptance.</param>
internal sealed record OpenTelemetryObservationOptionsSnapshot(
    ObservationExporterKey ExporterKey,
    ObservationExporterVersion ExporterVersion,
    OpenTelemetrySignalSet Signals,
    ObservationContentCapturePolicy ContentCapture,
    DataClassification ContentClassification,
    ImmutableHashSet<DataClassification> AllowedClassifications,
    ObservationDeliveryPolicy Delivery,
    ObservationBounds Bounds,
    bool AuditExporterProvidesDurableAcceptance)
{
    /// <summary>Compares every captured value, treating the allowed-classification set by membership.</summary>
    /// <param name="other">The snapshot to compare against.</param>
    /// <returns><see langword="true"/> when every captured value is equal.</returns>
    public bool Equals(OpenTelemetryObservationOptionsSnapshot? other) =>
        other is not null
        && ExporterKey == other.ExporterKey
        && ExporterVersion == other.ExporterVersion
        && Signals == other.Signals
        && ContentCapture == other.ContentCapture
        && ContentClassification == other.ContentClassification
        && AllowedClassifications.SetEquals(other.AllowedClassifications)
        && Delivery == other.Delivery
        && Bounds == other.Bounds
        && AuditExporterProvidesDurableAcceptance == other.AuditExporterProvidesDurableAcceptance;

    /// <summary>Hashes the same values <see cref="Equals(OpenTelemetryObservationOptionsSnapshot?)"/> compares.</summary>
    /// <returns>A hash code consistent with equality.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(ExporterKey);
        hash.Add(ExporterVersion);
        hash.Add(Signals);
        hash.Add(ContentCapture);
        hash.Add(ContentClassification);
        foreach (var classification in AllowedClassifications.Order())
        {
            hash.Add(classification);
        }

        hash.Add(Delivery);
        hash.Add(Bounds);
        hash.Add(AuditExporterProvidesDurableAcceptance);
        return hash.ToHashCode();
    }
}
