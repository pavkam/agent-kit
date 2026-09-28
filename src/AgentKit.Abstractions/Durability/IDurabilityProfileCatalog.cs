// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Resolves immutable durability profiles registered at composition time.</summary>
/// <remarks>
/// Implementations are engine-wide immutable singletons that are safe for concurrent reads. Lookup never activates a
/// backend, journal, lease manager, or recovery policy; it only reports the component keys a profile selected.
/// </remarks>
public interface IDurabilityProfileCatalog
{
    /// <summary>Attempts to resolve one named durability profile.</summary>
    /// <param name="key">The profile key to resolve.</param>
    /// <param name="profile">When this method returns <see langword="true"/>, the resolved immutable profile snapshot.</param>
    /// <returns><see langword="true"/> when <paramref name="key"/> is registered; otherwise <see langword="false"/>.</returns>
    public bool TryGet(DurabilityProfileKey key, [NotNullWhen(true)] out DurabilityProfileSnapshot? profile);
}
