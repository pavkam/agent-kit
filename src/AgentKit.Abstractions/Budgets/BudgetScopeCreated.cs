// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The scope was created, or an identical prior request with the same idempotency key already created it.</summary>
public sealed record BudgetScopeCreated: BudgetScopeResult
{
    /// <summary>Initializes a new instance of the <see cref="BudgetScopeCreated"/> record.</summary>
    /// <param name="scope">The created (or previously created) scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    public BudgetScopeCreated(IBudgetScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
    }

    /// <summary>Gets the created (or previously created) scope.</summary>
    public IBudgetScope Scope { get; init; }
}
