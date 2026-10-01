// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Stable default keys for first-party <see cref="ICompactor"/> registration.</summary>
/// <remarks>
/// Compaction is an optional capability, so the normative component selection has no compaction key: a definition
/// enables it through <see cref="AgentOptionalCapabilitySelection.CompactionProfile"/>. Run compilation resolves the
/// <see cref="ICompactor"/> registered under the compactor key the selected profile's
/// <see cref="ICompactionProfileCatalog"/> publication names; a definition that selects no profile uses the unkeyed
/// registration that <c>AddContextCompaction</c> exposes for this default key.
/// </remarks>
public static class AgentContextCompactionComponentDefaults
{
    /// <summary>Gets the default compactor key aligned with the default loop key.</summary>
    public static ComponentKey<ICompactor> CompactorKey =>
        new(AgentLoopComponentDefaults.LoopKey.Value);
}
