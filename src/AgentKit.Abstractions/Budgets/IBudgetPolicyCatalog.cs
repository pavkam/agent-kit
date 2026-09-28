// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Resolves keyed budget policies registered at composition time.</summary>
public interface IBudgetPolicyCatalog
{
    /// <summary>Attempts to resolve one named policy.</summary>
    /// <param name="key">The policy key to resolve.</param>
    /// <param name="policy">When this method returns <see langword="true"/>, the resolved policy implementation.</param>
    /// <returns><see langword="true"/> when <paramref name="key"/> is registered; otherwise <see langword="false"/>.</returns>
    public bool TryGet(BudgetPolicyKey key, [NotNullWhen(true)] out IBudgetPolicy? policy);
}
