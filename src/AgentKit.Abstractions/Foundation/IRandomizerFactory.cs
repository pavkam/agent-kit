// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Creates one operation-owned <see cref="IRandomizer"/> per operation and purpose.</summary>
/// <remarks>
/// The factory boundary keeps mutable random state from becoming an engine-wide singleton. Implementations are
/// thread-safe immutable singletons. The first-party default is cryptographically strong and reports a nondeterministic
/// descriptor; a deterministic test or replay replacement derives an isolated stream from its configured seed plus the
/// operation and purpose. Runtime code never calls an ambient random API when the result affects observable behavior.
/// </remarks>
public interface IRandomizerFactory
{
    /// <summary>Creates a new randomizer owned by the requesting operation.</summary>
    /// <param name="request">The validated operation and purpose.</param>
    /// <returns>A new randomizer the caller owns and must not share across parallel branches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    public IRandomizer Create(RandomizerCreationRequest request);
}
