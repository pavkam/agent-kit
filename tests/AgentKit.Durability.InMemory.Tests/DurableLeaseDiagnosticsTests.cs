// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

public sealed class DurableLeaseDiagnosticsTests
{
    [Fact]
    public void ToStableValue_WhenAcquisitionOutcomeIsUndefined_RejectsExactArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Enum.Parse<DurableLeaseAcquisitionOutcome>("42").ToStableValue());

    [Fact]
    public void ToStableValue_WhenAcquisitionOutcomeIsDefined_ReturnsOneDistinctStableValueEach()
    {
        DurableLeaseAcquisitionOutcome.GrantedFirstOwnership.ToStableValue().ShouldBe("granted_first_ownership");
        DurableLeaseAcquisitionOutcome.GrantedByTakeover.ToStableValue().ShouldBe("granted_by_takeover");
        DurableLeaseAcquisitionOutcome.HeldByAnotherWorker.ToStableValue().ShouldBe("held_by_another_worker");
        DurableLeaseAcquisitionOutcome.Cancelled.ToStableValue().ShouldBe("cancelled");
        DurableLeaseAcquisitionOutcome.Failed.ToStableValue().ShouldBe("failed");
    }

    [Fact]
    public void ToStableValue_WhenRenewalOutcomeIsUndefined_RejectsExactArgument() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Enum.Parse<DurableLeaseRenewalOutcome>("42").ToStableValue());

    [Fact]
    public void ToStableValue_WhenRenewalOutcomeIsDefined_ReturnsOneDistinctStableValueEach()
    {
        DurableLeaseRenewalOutcome.Renewed.ToStableValue().ShouldBe("renewed");
        DurableLeaseRenewalOutcome.Lost.ToStableValue().ShouldBe("lost");
        DurableLeaseRenewalOutcome.Cancelled.ToStableValue().ShouldBe("cancelled");
        DurableLeaseRenewalOutcome.Failed.ToStableValue().ShouldBe("failed");
    }

    [Fact]
    public void RecordAcquisition_WhenArgumentsAreInvalid_ThrowsBeforePublishingMeasurements()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
                DurableLeaseMetrics.RecordAcquisition(Enum.Parse<DurableLeaseAcquisitionOutcome>("42"), null))
            .ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentOutOfRangeException>(() =>
                DurableLeaseMetrics.RecordAcquisition(DurableLeaseAcquisitionOutcome.GrantedFirstOwnership, TimeSpan.FromTicks(-1)))
            .ParamName.ShouldBe("elapsed");
    }

    [Fact]
    public void RecordRenewal_WhenArgumentsAreInvalid_ThrowsBeforePublishingMeasurements()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
                DurableLeaseMetrics.RecordRenewal(Enum.Parse<DurableLeaseRenewalOutcome>("42"), null))
            .ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentOutOfRangeException>(() =>
                DurableLeaseMetrics.RecordRenewal(DurableLeaseRenewalOutcome.Renewed, TimeSpan.FromTicks(-1)))
            .ParamName.ShouldBe("elapsed");
    }

    [Fact]
    public void RecordRelease_WhenCalled_DoesNotThrow() => Should.NotThrow(DurableLeaseMetrics.RecordRelease);
}
