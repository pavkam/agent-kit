// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using System.Diagnostics.CodeAnalysis;

/// <summary>An immutable <see cref="IBudgetProfileCatalog"/> built from registered profile contributors.</summary>
internal sealed class InMemoryBudgetProfileCatalog: IBudgetProfileCatalog
{
    private readonly BudgetProfileRegistry _registry;

    /// <summary>Initializes the catalog over a populated profile registry.</summary>
    /// <param name="registry">The registry initialized from every profile contributor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public InMemoryBudgetProfileCatalog(BudgetProfileRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public bool TryGet(BudgetProfileKey key, [NotNullWhen(true)] out BudgetProfileSnapshot? profile) =>
        _registry.TryGet(key, out profile);
}
