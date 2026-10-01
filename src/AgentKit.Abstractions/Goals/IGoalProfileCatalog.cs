// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Publishes validated, immutable goal-profile snapshots by key.</summary>
/// <remarks>The catalog resolves the latest published revision of a key. Durable goals carry the revision they captured, and <see cref="TryGet(GoalProfileReference, out GoalProfileSnapshot)"/> resolves exactly that revision or none. Implementations are thread-safe.</remarks>
public interface IGoalProfileCatalog
{
    /// <summary>Resolves the latest published revision of a profile.</summary>
    /// <param name="key">The profile key.</param>
    /// <param name="profile">The snapshot when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the key is published.</returns>
    public bool TryGet(GoalProfileKey key, [NotNullWhen(true)] out GoalProfileSnapshot? profile);

    /// <summary>Resolves exactly one captured profile revision.</summary>
    /// <param name="reference">The captured key and version.</param>
    /// <param name="profile">The snapshot when that exact revision is published; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the published revision equals the captured version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    public bool TryGet(GoalProfileReference reference, [NotNullWhen(true)] out GoalProfileSnapshot? profile);
}
