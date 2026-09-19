// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Verifies <see cref="UsageAccounting.BuildEntry"/>'s mapping from a model response's reported usage.</summary>
public sealed class UsageAccountingTests
{
    private static readonly RunId _runId = new(Guid.NewGuid());
    private static readonly OperationId _operationId = new(Guid.NewGuid());
    private static readonly ModelRequestId _requestId = new(Guid.NewGuid());
    private static readonly UsageEntryId _entryId = new(Guid.NewGuid());
    private static readonly ModelDescriptor _model = TestFactory.Model();

    [Fact]
    public void BuildEntry_WhenUsageIsNotReported_ReturnsNull() =>
        UsageAccounting.BuildEntry(_entryId, _runId, _operationId, _model, _requestId, ModelUsage.NotReported).ShouldBeNull();

    [Fact]
    public void BuildEntry_WhenEveryCounterIsAbsent_ReturnsNull()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, null, null, null, null, null, null, ExtensionData.Empty);
        UsageAccounting.BuildEntry(_entryId, _runId, _operationId, _model, _requestId, usage).ShouldBeNull();
    }

    [Fact]
    public void BuildEntry_WhenTokensAreReported_MapsEveryDimension()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 100, 20, 30, 5, null, null, ExtensionData.Empty);
        var entry = UsageAccounting.BuildEntry(_entryId, _runId, _operationId, _model, _requestId, usage);

        _ = entry.ShouldNotBeNull();
        entry.Id.ShouldBe(_entryId);
        entry.RunId.ShouldBe(_runId);
        entry.OperationId.ShouldBe(_operationId);
        entry.Revision.ShouldBe(new UsageAccountingRevision(1));
        entry.PreviousRevision.ShouldBeNull();
        entry.Model.ShouldBe(new ModelUsageAttribution(_requestId, _model.ProviderId, _model.ApiFamily, _model.ModelId, _model.DeploymentId));
        entry.ProviderUsage.ShouldBe(usage);
        entry.Measurements.Length.ShouldBe(4);
        entry.Measurements.ShouldContain(m =>
            m.Dimension == BudgetDimensions.InputTokens && m.Unit == new BudgetUnit("tokens")
            && m.Amount == BudgetQuantity.FromDecimal(100m) && m.Quality == UsageMeasurementQuality.ProviderReported);
        entry.Measurements.ShouldContain(m => m.Dimension == BudgetDimensions.OutputTokens && m.Amount == BudgetQuantity.FromDecimal(20m));
        entry.Measurements.ShouldContain(m => m.Dimension == BudgetDimensions.CachedReadTokens && m.Amount == BudgetQuantity.FromDecimal(30m));
        entry.Measurements.ShouldContain(m => m.Dimension == BudgetDimensions.ReasoningTokens && m.Amount == BudgetQuantity.FromDecimal(5m));
    }

    [Fact]
    public void BuildEntry_WhenCostAndCurrencyAreReported_AddsACostMeasurementInThatCurrency()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 1, null, null, null, 0.02m, "EUR", ExtensionData.Empty);
        var entry = UsageAccounting.BuildEntry(_entryId, _runId, _operationId, _model, _requestId, usage);

        _ = entry.ShouldNotBeNull();
        entry.Measurements.ShouldContain(m =>
            m.Dimension == BudgetDimensions.Cost && m.Unit == new BudgetUnit("eur")
            && m.Amount == BudgetQuantity.FromDecimal(0.02m) && m.Quality == UsageMeasurementQuality.ProviderReported);
    }

    [Fact]
    public void BuildEntry_WhenCostIsReportedWithoutCurrency_OmitsTheCostMeasurement()
    {
        var usage = new ModelUsage(ModelUsageReportState.Final, 1, null, null, null, 0.02m, null, ExtensionData.Empty);
        var entry = UsageAccounting.BuildEntry(_entryId, _runId, _operationId, _model, _requestId, usage);

        _ = entry.ShouldNotBeNull();
        entry.Measurements.ShouldNotContain(m => m.Dimension == BudgetDimensions.Cost);
    }
}
