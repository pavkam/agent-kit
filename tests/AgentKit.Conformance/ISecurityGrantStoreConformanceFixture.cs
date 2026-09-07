// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates isolated security-grant-store composition for one conformance case and controls every created store's clock.</summary>
/// <remarks>
/// A suite creates and disposes one fixture per inherited case. Repeated <see cref="CreateAsync"/> calls within that fixture
/// lifetime must use the same controllable clock and may return the same composed store when the implementation's normal DI
/// lifetime does so. Advancing time affects every store created by this fixture and never relies on an ambient clock.
/// </remarks>
public interface ISecurityGrantStoreConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets the deterministic clock supplied to every store composed by this fixture.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Creates the store under test through the implementation's public registration path for this fixture's one contract case.</summary>
    /// <param name="cancellationToken">Cancels before fixture composition completes.</param>
    /// <returns>The composed public grant-store contract.</returns>
    public ValueTask<ISecurityGrantStore> CreateAsync(CancellationToken cancellationToken = default);

    /// <summary>Advances the deterministic clock observed by every store composed through this fixture.</summary>
    /// <param name="duration">The amount of simulated time to elapse.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is not positive.</exception>
    public void Advance(TimeSpan duration);
}
