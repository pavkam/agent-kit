// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Produces the default <see cref="BudgetDimensionDescriptor"/> set for every first-party <c>agentkit.*</c> dimension.</summary>
/// <remarks>
/// These defaults are registered by <c>AddAgentBudgets</c> and are
/// replaceable: an application that needs a different unit or aggregation
/// for a first-party dimension (for example, a cost dimension expressed in
/// a currency other than <c>"usd"</c>) calls <c>ReplaceBudgetDimension</c>
/// with the same <see cref="BudgetDimension"/> key.
/// </remarks>
internal static class BudgetDimensionCatalogDefaults
{
    private static readonly BudgetUnit _count = new("count");
    private static readonly BudgetUnit _tokens = new("tokens");
    private static readonly BudgetUnit _usd = new("usd");
    private static readonly BudgetUnit _seconds = new("seconds");
    private static readonly BudgetUnit _bytes = new("bytes");

    /// <summary>Gets the default descriptor set, keyed by dimension.</summary>
    public static ImmutableArray<BudgetDimensionDescriptor> Create() =>
    [
        Sum(BudgetDimensions.Turns, _count),
        Sum(BudgetDimensions.Steps, _count),
        Sum(BudgetDimensions.ModelRequests, _count),
        Gauge(BudgetDimensions.ConcurrentModelRequests, _count),
        Sum(BudgetDimensions.InputTokens, _tokens),
        Sum(BudgetDimensions.OutputTokens, _tokens),
        Sum(BudgetDimensions.ReasoningTokens, _tokens),
        Sum(BudgetDimensions.CachedReadTokens, _tokens),
        Sum(BudgetDimensions.CachedWriteTokens, _tokens),
        Sum(BudgetDimensions.PerRequestInputTokens, _tokens),
        Sum(BudgetDimensions.PerRequestOutputTokens, _tokens),
        Sum(BudgetDimensions.Cost, _usd),
        Sum(BudgetDimensions.AttemptedToolCalls, _count),
        Sum(BudgetDimensions.SuccessfulToolCalls, _count),
        Gauge(BudgetDimensions.ConcurrentToolCalls, _count),
        Sum(BudgetDimensions.ToolRetries, _count),
        Sum(BudgetDimensions.OutputValidationRetries, _count),
        Sum(BudgetDimensions.Delegations, _count),
        Gauge(BudgetDimensions.ConcurrentDelegations, _count),
        Duration(BudgetDimensions.RunElapsedTime, _seconds),
        Duration(BudgetDimensions.OperationElapsedTime, _seconds),
        Sum(BudgetDimensions.ContextBytes, _bytes),
        Sum(BudgetDimensions.ContextTokens, _tokens),
        Sum(BudgetDimensions.RetainedMediaBytes, _bytes),
        Sum(BudgetDimensions.QueuedInputCount, _count),
        Sum(BudgetDimensions.QueuedInputBytes, _bytes),
        Duration(BudgetDimensions.QueuedInputAge, _seconds),
        Sum(BudgetDimensions.EventBytes, _bytes),
        Sum(BudgetDimensions.ResultBytes, _bytes),
        Sum(BudgetDimensions.ToolResultBytes, _bytes),
        Sum(BudgetDimensions.RetrievedItemCount, _count),
        Sum(BudgetDimensions.RetrievedBytes, _bytes),
        Sum(BudgetDimensions.BufferedStreamBytes, _bytes),
        Sum(BudgetDimensions.ArtifactBytes, _bytes),
    ];

    private static BudgetDimensionDescriptor Sum(BudgetDimension dimension, BudgetUnit unit) =>
        new(dimension, BudgetAggregationKind.Sum, [unit]);

    private static BudgetDimensionDescriptor Gauge(BudgetDimension dimension, BudgetUnit unit) =>
        new(dimension, BudgetAggregationKind.ConcurrentGauge, [unit]);

    private static BudgetDimensionDescriptor Duration(BudgetDimension dimension, BudgetUnit unit) =>
        new(dimension, BudgetAggregationKind.Duration, [unit]);
}
