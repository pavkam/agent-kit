// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryProposalResult"/> factories.</summary>
public sealed class MemoryProposalResultTests
{
    [Fact]
    public void Accepted_WhenRecordIsSupplied_ReportsAccepted()
    {
        var record = MemoryTestData.Record(MemoryTestData.NewOwner());

        var result = MemoryProposalResult.Accepted(record, replayed: true);

        result.IsAccepted.ShouldBeTrue();
        result.Outcome.ShouldBe(MemoryProposalOutcome.Accepted);
        result.Record.ShouldBe(record);
        result.Replayed.ShouldBeTrue();
        result.Denial.ShouldBeNull();
        result.Failure.ShouldBeNull();
    }

    [Fact]
    public void PolicyDenied_WhenDenialIsSupplied_CarriesNoRecord()
    {
        var denial = new MemoryPolicyDenied(new ComponentId("p"), "c", "m");

        var result = MemoryProposalResult.PolicyDenied(denial);

        result.IsAccepted.ShouldBeFalse();
        result.Outcome.ShouldBe(MemoryProposalOutcome.PolicyDenied);
        result.Denial.ShouldBe(denial);
        result.Record.ShouldBeNull();
    }

    [Fact]
    public void Rejected_WhenFailureIsSupplied_CarriesNoRecord()
    {
        var failure = new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, "down");

        var result = MemoryProposalResult.Rejected(failure);

        result.Outcome.ShouldBe(MemoryProposalOutcome.Rejected);
        result.Failure.ShouldBe(failure);
        result.Record.ShouldBeNull();
    }

    [Fact]
    public void Factories_WhenGivenNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => MemoryProposalResult.Accepted(null!, false)).ParamName.ShouldBe("record");
        Should.Throw<ArgumentNullException>(() => MemoryProposalResult.PolicyDenied(null!)).ParamName.ShouldBe("denial");
        Should.Throw<ArgumentNullException>(() => MemoryProposalResult.Rejected(null!)).ParamName.ShouldBe("failure");
    }
}
