// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

/// <summary>Verifies value equality of the immutable exporter snapshot.</summary>
public sealed class OpenTelemetryObservationOptionsSnapshotTests
{
    [Fact]
    public void Equals_WhenAllowedClassificationsHaveTheSameMembers_IsEqualRegardlessOfInstance()
    {
        var left = Snapshot([DataClassification.Public, DataClassification.Internal]);
        var right = Snapshot([DataClassification.Internal, DataClassification.Public]);

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_WhenAllowedClassificationsDiffer_IsNotEqual() =>
        Snapshot([DataClassification.Public]).Equals(Snapshot([DataClassification.Internal])).ShouldBeFalse();

    [Fact]
    public void Equals_WhenOtherIsNull_IsNotEqual() => Snapshot([DataClassification.Public]).Equals(null).ShouldBeFalse();

    [Fact]
    public void Equals_WhenAnyOtherCapturedValueDiffers_IsNotEqual()
    {
        var baseline = Snapshot([DataClassification.Public]);

        (baseline with { ExporterVersion = new ObservationExporterVersion(2) }).Equals(baseline).ShouldBeFalse();
        (baseline with { Signals = OpenTelemetrySignalSet.Logs }).Equals(baseline).ShouldBeFalse();
        (baseline with { ContentCapture = new ObservationContentCapturePolicy(true) }).Equals(baseline).ShouldBeFalse();
        (baseline with { ContentClassification = DataClassification.Public }).Equals(baseline).ShouldBeFalse();
        (baseline with { Delivery = new ObservationDeliveryPolicy(true, TimeSpan.FromSeconds(1)) }).Equals(baseline).ShouldBeFalse();
        (baseline with { Bounds = new ObservationBounds(1) }).Equals(baseline).ShouldBeFalse();
        (baseline with { AuditExporterProvidesDurableAcceptance = true }).Equals(baseline).ShouldBeFalse();
        (baseline with { ExporterKey = new ObservationExporterKey("other") }).Equals(baseline).ShouldBeFalse();
    }

    private static OpenTelemetryObservationOptionsSnapshot Snapshot(ImmutableHashSet<DataClassification> allowed) =>
        new(
            new ObservationExporterKey("key"),
            new ObservationExporterVersion(1),
            OpenTelemetrySignalSet.Activities,
            new ObservationContentCapturePolicy(),
            DataClassification.Confidential,
            allowed,
            new ObservationDeliveryPolicy(),
            new ObservationBounds(64),
            AuditExporterProvidesDurableAcceptance: false);
}
