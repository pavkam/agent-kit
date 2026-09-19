// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Maps one provider-reported <see cref="ModelUsage"/> response into this run's usage-accounting evidence.</summary>
/// <remarks>
/// This mirrors <see cref="RunBudget.AccountUsageAsync"/>'s dimension mapping for tokens and cost, but as a
/// historical record rather than a reservation: it also retains cached-read tokens (no budget dimension reserves
/// them today) and accepts any reported cost currency rather than only <c>"usd"</c>, since an accounting entry
/// makes no enforcement decision and can preserve whatever the provider actually reported.
/// </remarks>
internal static class UsageAccounting
{
    private static readonly BudgetUnit _tokens = new("tokens");

    /// <summary>Builds one usage-accounting entry from a model response, when it reports any known measurement.</summary>
    /// <param name="id">The fresh identity allocated for this entry.</param>
    /// <param name="runId">The run the response belongs to.</param>
    /// <param name="operationId">The turn's causal operation identity.</param>
    /// <param name="model">The resolved model descriptor the response was produced by.</param>
    /// <param name="requestId">The logical model request identity this response answers.</param>
    /// <param name="usage">The response's reported usage.</param>
    /// <returns>
    /// A new entry carrying every dimension the provider reported, or <see langword="null"/> when the provider
    /// reported nothing at all (<see cref="ModelUsageReportState.NotReported"/> or every counter absent).
    /// </returns>
    public static UsageAccountingEntry? BuildEntry(
        UsageEntryId id, RunId runId, OperationId operationId, ModelDescriptor model, ModelRequestId requestId, ModelUsage usage)
    {
        Debug.Assert(model is not null, "A resolved model descriptor is required to attribute usage.");
        Debug.Assert(usage is not null, "A model response always carries a usage value, even if NotReported.");
        if (usage.ReportState == ModelUsageReportState.NotReported)
        {
            return null;
        }

        var measurements = ImmutableArray.CreateBuilder<UsageMeasurement>();
        AddTokenMeasurement(measurements, BudgetDimensions.InputTokens, usage.InputTokens);
        AddTokenMeasurement(measurements, BudgetDimensions.OutputTokens, usage.OutputTokens);
        AddTokenMeasurement(measurements, BudgetDimensions.ReasoningTokens, usage.ReasoningTokens);
        AddTokenMeasurement(measurements, BudgetDimensions.CachedReadTokens, usage.CachedInputTokens);
        if (usage.EstimatedCost is { } cost && usage.CostCurrency is { } currency)
        {
            measurements.Add(new UsageMeasurement(
                BudgetDimensions.Cost,
                new BudgetUnit(currency.ToLowerInvariant()),
                BudgetQuantity.FromDecimal(cost),
                UsageMeasurementQuality.ProviderReported));
        }

        if (measurements.Count == 0)
        {
            return null;
        }

        var attribution = new ModelUsageAttribution(requestId, model.ProviderId, model.ApiFamily, model.ModelId, model.DeploymentId);
        return new UsageAccountingEntry(
            id, runId, operationId, new UsageAccountingRevision(1), previousRevision: null,
            measurements.ToImmutable(), attribution, usage, ExtensionData.Empty);
    }

    private static void AddTokenMeasurement(
        ImmutableArray<UsageMeasurement>.Builder measurements, BudgetDimension dimension, long? amount)
    {
        if (amount is { } value)
        {
            measurements.Add(new UsageMeasurement(
                dimension, _tokens, BudgetQuantity.FromDecimal(value), UsageMeasurementQuality.ProviderReported));
        }
    }
}
