// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="ICompactionProfileCatalog"/> test double over a fixed set of publications.</summary>
public sealed class StaticCompactionProfileCatalog: ICompactionProfileCatalog
{
    private readonly Dictionary<CompactionProfileKey, CompactionProfilePublication> _profiles;

    /// <summary>Initializes the catalog over the publications it resolves.</summary>
    /// <param name="profiles">The publications that resolve, keyed by their own profile key; may be empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profiles"/> is null.</exception>
    public StaticCompactionProfileCatalog(params CompactionProfilePublication[] profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        _profiles = profiles.ToDictionary(static profile => profile.ProfileKey);
    }

    /// <inheritdoc/>
    public bool TryGet(CompactionProfileKey key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CompactionProfilePublication? profile) =>
        _profiles.TryGetValue(key, out profile);
}
