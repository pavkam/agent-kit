// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Tests;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentBudgets_WhenLedgerSelected_RegistersLedgerBackedBudgetAuthority()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentBudgets();
        _ = services.AddSingleton<IBudgetLedger, RecordingBudgetLedger>();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IBudgetAuthority>().ShouldBeOfType<BudgetAuthority>();
    }

    [Fact]
    public void AddAgentBudgets_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentBudgets();
        _ = services.AddAgentBudgets();
        _ = services.AddSingleton<IBudgetLedger, RecordingBudgetLedger>();

        using var provider = services.BuildServiceProvider();
        provider.GetServices<IBudgetAuthority>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddAgentBudgets_RegistersFirstPartyDimensionCatalog()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentBudgets();

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IBudgetDimensionCatalog>();
        catalog.TryGet(BudgetDimensions.InputTokens, out var descriptor).ShouldBeTrue();
        descriptor!.Aggregation.ShouldBe(BudgetAggregationKind.Sum);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddAgentBudgets_WhenMaximumScopeDepthIsNotPositive_FailsValidationOnAccess(int depth)
    {
        var services = new ServiceCollection();

        _ = services.AddAgentBudgets(options => options.MaximumScopeDepth = depth);
        _ = services.AddSingleton<IBudgetLedger, RecordingBudgetLedger>();

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<IBudgetAuthority>);
    }

    [Fact]
    public void AddBudgetDimension_WhenDuplicateDimensionRegistered_FailsCatalogConstruction()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentBudgets();
        _ = services.AddBudgetDimension(new BudgetDimensionDescriptor(
            BudgetDimensions.InputTokens, BudgetAggregationKind.Sum, [new BudgetUnit("tokens")]));

        using var provider = services.BuildServiceProvider();
        _ = Should.Throw<ArgumentException>(provider.GetRequiredService<IBudgetDimensionCatalog>);
    }

    [Fact]
    public void ReplaceBudgetDimension_ReplacesExistingDescriptorForSameDimension()
    {
        var services = new ServiceCollection();
        var replacement = new BudgetDimensionDescriptor(
            BudgetDimensions.Cost, BudgetAggregationKind.Sum, [new BudgetUnit("eur")]);

        _ = services.AddAgentBudgets();
        _ = services.ReplaceBudgetDimension(replacement);

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<IBudgetDimensionCatalog>();
        catalog.TryGet(BudgetDimensions.Cost, out var descriptor).ShouldBeTrue();
        descriptor!.AllowedUnits.ShouldBe([new BudgetUnit("eur")]);
    }

    [Fact]
    public void ReplaceBudgetAuthority_ReplacesRegisteredAuthority()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentBudgets();
        _ = services.ReplaceBudgetAuthority<FakeBudgetAuthority>();

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IBudgetAuthority>().ShouldBeOfType<FakeBudgetAuthority>();
    }

    [Fact]
    public void AddAgentBudgets_WhenNoLedgerSelected_FailsBeforeLedgerOperation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentBudgets();

        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(provider.GetRequiredService<IBudgetAuthority>);
        exception.Message.ShouldContain("exactly one");
    }

    [Fact]
    public void AddAgentBudgets_WhenTwoLedgersSelected_FailsBeforeLedgerOperation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentBudgets();
        _ = services.AddSingleton<IBudgetLedger, RecordingBudgetLedger>();
        _ = services.AddSingleton<IBudgetLedger, RecordingBudgetLedger>();

        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<InvalidOperationException>(provider.GetRequiredService<IBudgetAuthority>);
        exception.Message.ShouldContain("exactly one");
    }

    [Fact]
    public void AddBudgetDimension_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentNullException>(() => services.AddBudgetDimension(null!));
    }

    [Fact]
    public void ReplaceBudgetDimension_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<ArgumentNullException>(() => services.ReplaceBudgetDimension(null!));
    }

    private sealed class FakeBudgetAuthority: IBudgetAuthority
    {
        public ValueTask<BudgetScopeResult> CreateChildScopeAsync(
            BudgetScopeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
