// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

/// <summary>Verifies immutable budget-ledger capability declarations.</summary>
public sealed class BudgetLedgerDescriptorTests
{
    [Theory]
    [InlineData(false, BudgetLedgerConcurrencyDomain.ProcessLocal)]
    [InlineData(true, BudgetLedgerConcurrencyDomain.ProcessLocal)]
    [InlineData(false, BudgetLedgerConcurrencyDomain.HostLocal)]
    [InlineData(true, BudgetLedgerConcurrencyDomain.HostLocal)]
    [InlineData(false, BudgetLedgerConcurrencyDomain.Distributed)]
    [InlineData(true, BudgetLedgerConcurrencyDomain.Distributed)]
    public void Constructor_WhenCombinationDefined_PreservesOrthogonalEvidence(
        bool durable,
        BudgetLedgerConcurrencyDomain concurrencyDomain)
    {
        var descriptor = new BudgetLedgerDescriptor(durable, concurrencyDomain);

        descriptor.Durable.ShouldBe(durable);
        descriptor.ConcurrencyDomain.ShouldBe(concurrencyDomain);
        descriptor.ShouldBe(new BudgetLedgerDescriptor(durable, concurrencyDomain));
    }

    [Fact]
    public void Constructor_WhenConcurrencyDomainUndefined_ThrowsWithExactParameterName()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new BudgetLedgerDescriptor(false, (BudgetLedgerConcurrencyDomain) 99));

        exception.ParamName.ShouldBe("concurrencyDomain");
    }
}
