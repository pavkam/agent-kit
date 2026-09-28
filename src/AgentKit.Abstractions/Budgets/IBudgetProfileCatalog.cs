// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Resolves immutable budget profiles registered at composition time.</summary>
public interface IBudgetProfileCatalog
{
    /// <summary>Attempts to resolve one named profile.</summary>
    /// <param name="key">The profile key to resolve.</param>
    /// <param name="profile">When this method returns <see langword="true"/>, the resolved profile snapshot.</param>
    /// <returns><see langword="true"/> when <paramref name="key"/> is registered; otherwise <see langword="false"/>.</returns>
    public bool TryGet(BudgetProfileKey key, [NotNullWhen(true)] out BudgetProfileSnapshot? profile);
}
