// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Resolves immutable compaction profiles published at composition time.</summary>
/// <remarks>
/// The catalog is the neutral read side of compaction profile registration. Composition validation and run-plan
/// compilation read it to learn which compactor a <see cref="AgentOptionalCapabilitySelection.CompactionProfile"/>
/// selects and which policy its requests carry, without referencing the package that registered the profile. Lookups
/// are pure reads over a published snapshot: they perform no I/O, resolve no service, and are safe to call
/// concurrently.
/// </remarks>
public interface ICompactionProfileCatalog
{
    /// <summary>Attempts to resolve one published profile.</summary>
    /// <param name="key">The profile key to resolve.</param>
    /// <param name="profile">When this method returns <see langword="true"/>, the published profile.</param>
    /// <returns><see langword="true"/> when <paramref name="key"/> is published; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is the default value.</exception>
    public bool TryGet(CompactionProfileKey key, [NotNullWhen(true)] out CompactionProfilePublication? profile);
}
