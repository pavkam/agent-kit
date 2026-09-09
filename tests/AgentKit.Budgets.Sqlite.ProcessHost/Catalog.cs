// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.ProcessHost;

/// <summary>Provides the one deterministic sum dimension needed by the abrupt-loss fixture.</summary>
internal sealed class Catalog: IBudgetDimensionCatalog
{
    /// <inheritdoc/>
    public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
    {
        descriptor = dimension == new BudgetDimension("test.sum")
            ? new(dimension, BudgetAggregationKind.Sum, [new("count")])
            : null;
        return descriptor is not null;
    }
}
