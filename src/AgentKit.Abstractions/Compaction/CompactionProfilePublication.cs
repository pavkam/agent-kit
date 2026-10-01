// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One immutable compaction profile as published to an <see cref="ICompactionProfileCatalog"/>.</summary>
/// <remarks>
/// A publication is declarative evidence captured at composition time: it names the compactor the profile selects, the
/// compiled <see cref="CompactionPolicySnapshot"/> every request under the profile carries, and whether compaction is
/// enabled. It holds no service, session, or authority, and replacing a catalog registration never changes a
/// publication a run plan already captured.
/// </remarks>
public sealed record CompactionProfilePublication
{
    /// <summary>Initializes a validated profile publication.</summary>
    /// <param name="policy">The non-null compiled policy; its profile and compactor keys identify the publication.</param>
    /// <param name="enabled">
    /// Whether compaction runs under this profile. A disabled profile stays selectable and validated, but the engine
    /// composes no compactor for agents that select it.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is <see langword="null"/>.</exception>
    public CompactionProfilePublication(CompactionPolicySnapshot policy, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Policy = policy;
        Enabled = enabled;
    }

    /// <summary>Gets the compiled policy every request under this profile carries.</summary>
    /// <value>A non-null immutable snapshot.</value>
    public CompactionPolicySnapshot Policy { get; }

    /// <summary>Gets a value indicating whether compaction runs under this profile.</summary>
    /// <value><see langword="false"/> when the profile deliberately disables compaction for agents that select it.</value>
    public bool Enabled { get; }

    /// <summary>Gets the profile key that owns this publication.</summary>
    /// <value>The nondefault key from <see cref="Policy"/>.</value>
    public CompactionProfileKey ProfileKey => Policy.ProfileKey;

    /// <summary>Gets the compactor key the profile selects.</summary>
    /// <value>The nondefault key from <see cref="Policy"/>.</value>
    public ComponentKey<ICompactor> CompactorKey => Policy.CompactorKey;
}
