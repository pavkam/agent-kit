// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Resolves keyed <see cref="IBudgetPolicy"/> registrations from the host provider.</summary>
internal sealed class InMemoryBudgetPolicyCatalog: IBudgetPolicyCatalog
{
    private readonly IServiceProvider _provider;

    /// <summary>Initializes the policy catalog.</summary>
    /// <param name="provider">The host provider used to read keyed policy registrations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public InMemoryBudgetPolicyCatalog(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc/>
    public bool TryGet(BudgetPolicyKey key, [NotNullWhen(true)] out IBudgetPolicy? policy)
    {
        policy = null;
        if (key.Value is not { Length: > 0 })
        {
            return false;
        }

        policy = _provider.GetKeyedService<IBudgetPolicy>(key.Value);
        return policy is not null;
    }
}
