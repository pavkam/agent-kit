// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Binds a resolution replay key to exact immutable request evidence and outcome.</summary>
internal sealed class OverrunResolutionState
{
    /// <summary>Creates replay state.</summary><param name="request">The exact request.</param><param name="result">The immutable result.</param><exception cref="ArgumentNullException">An argument is null.</exception>
    internal OverrunResolutionState(BudgetOverrunHoldResolutionRequest request, BudgetOverrunHoldResolutionResult result) { ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(result); Request = request; Result = result; }
    /// <summary>Gets exact caller evidence.</summary><value>The request used for conflict detection.</value>
    internal BudgetOverrunHoldResolutionRequest Request { get; }
    /// <summary>Gets the immutable outcome.</summary><value>The exact replay result.</value>
    internal BudgetOverrunHoldResolutionResult Result { get; }
}
