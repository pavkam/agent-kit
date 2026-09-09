// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed terminal outcome of attempting to start a reservation.</summary>
/// <remarks>
/// Concrete outcomes are <see cref="BudgetStarted"/>, <see cref="BudgetStartRejected"/>,
/// and <see cref="BudgetStartExpired"/>. Rejection and expiration are normal budget outcomes and
/// guarantees that the caller must not begin the protected effect.
/// </remarks>
public abstract record BudgetStartResult
{
    /// <summary>Initializes the closed result hierarchy.</summary>
    private protected BudgetStartResult()
    {
    }
}
