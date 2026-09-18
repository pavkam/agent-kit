// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Binds one operator-resolution replay key to its exact immutable request evidence and outcome.</summary>
/// <remarks>
/// Both blocked and resolved outcomes are retained, because a replay key is bound by the attempt rather than by its
/// success. The binding is reconstructed from the resolution's journal record so an exact retry after process loss returns
/// the original receipt instead of re-evaluating accounting that may since have changed.
/// </remarks>
internal sealed class OverrunResolutionState
{
    /// <summary>Creates replay state after the resolution attempt is durably recorded.</summary>
    /// <param name="request">The exact non-null caller request, including its audit receipt.</param>
    /// <param name="result">The immutable non-null outcome returned by every exact replay.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    internal OverrunResolutionState(
        BudgetOverrunHoldResolutionRequest request, BudgetOverrunHoldResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        Request = request;
        Result = result;
    }

    /// <summary>Gets exact caller evidence.</summary>
    /// <value>The request compared against a retry to detect a reused key carrying different evidence.</value>
    internal BudgetOverrunHoldResolutionRequest Request { get; }

    /// <summary>Gets the immutable outcome.</summary>
    /// <value>The original blocked or resolved receipt, returned unchanged even after accounting moves on.</value>
    internal BudgetOverrunHoldResolutionResult Result { get; }
}
