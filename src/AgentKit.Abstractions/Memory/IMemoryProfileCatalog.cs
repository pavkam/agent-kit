// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Resolves the immutable memory profile snapshots composition compiled.</summary>
/// <remarks>The catalog is discovery only. It holds snapshots, never collaborators, and resolving a profile grants nothing.</remarks>
public interface IMemoryProfileCatalog
{
    /// <summary>Resolves a profile by key.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="profile">When this method returns <see langword="true"/>, the snapshot.</param>
    /// <returns><see langword="true"/> when the profile is registered; otherwise <see langword="false"/>.</returns>
    public bool TryGet(MemoryProfileKey key, [NotNullWhen(true)] out MemoryProfileSnapshot? profile);

    /// <summary>Resolves a profile by key and exact version.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="version">The exact version.</param>
    /// <param name="profile">When this method returns <see langword="true"/>, the snapshot.</param>
    /// <returns><see langword="true"/> when the profile is registered at exactly that version; otherwise <see langword="false"/>.</returns>
    public bool TryGet(MemoryProfileKey key, MemoryProfileVersion version, [NotNullWhen(true)] out MemoryProfileSnapshot? profile);
}
