// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IBudgetAuthority"/> test double that throws <see cref="NotSupportedException"/> when a scope is requested.</summary>
/// <remarks>A placeholder collaborator for tests whose scripted loop never creates a run budget scope.</remarks>
public sealed class UnsupportedBudgetAuthority: IBudgetAuthority
{
    /// <inheritdoc/>
    public ValueTask<BudgetScopeResult> CreateChildScopeAsync(
        BudgetScopeRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support budget scopes.");
}
