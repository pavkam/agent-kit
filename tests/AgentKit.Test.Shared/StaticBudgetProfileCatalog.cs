// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IBudgetProfileCatalog"/> test double that knows a fixed set of limit-free profiles.</summary>
/// <remarks>Each registered key resolves to a version-one profile with no limits and no policies.</remarks>
public sealed class StaticBudgetProfileCatalog: IBudgetProfileCatalog
{
    private readonly HashSet<BudgetProfileKey> _keys;

    /// <summary>Initializes the catalog over the profile keys it resolves.</summary>
    /// <param name="keys">The nonblank profile keys that resolve; may be empty to resolve nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is null.</exception>
    public StaticBudgetProfileCatalog(params BudgetProfileKey[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys = [.. keys];
    }

    /// <inheritdoc/>
    public bool TryGet(BudgetProfileKey key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetProfileSnapshot? profile)
    {
        if (_keys.Contains(key))
        {
            profile = new BudgetProfileSnapshot(key, new BudgetProfileVersion(1), [], [], new ContentHash("sha256:test-budget-profile"));
            return true;
        }

        profile = null;
        return false;
    }
}
