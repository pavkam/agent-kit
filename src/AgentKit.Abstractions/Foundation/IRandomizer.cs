// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One operation-owned source of random values.</summary>
/// <remarks>
/// A randomizer is not thread-safe, is never shared across parallel branches, is never registered in dependency
/// injection, and is not retained beyond the operation that created it. Create one per operation and purpose through
/// <see cref="IRandomizerFactory"/>.
/// </remarks>
public interface IRandomizer
{
    /// <summary>Gets how this randomizer produces values.</summary>
    /// <value>An immutable descriptor that never carries raw seed material.</value>
    public RandomizerDescriptor Descriptor { get; }

    /// <summary>Returns a value in <c>[0, exclusiveMaximum)</c>.</summary>
    /// <param name="exclusiveMaximum">The strictly positive exclusive upper bound.</param>
    /// <returns>A uniformly distributed value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exclusiveMaximum"/> is not positive.</exception>
    public int NextInt32(int exclusiveMaximum);

    /// <summary>Returns a value in <c>[0, exclusiveMaximum)</c>.</summary>
    /// <param name="exclusiveMaximum">The strictly positive exclusive upper bound.</param>
    /// <returns>A uniformly distributed value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exclusiveMaximum"/> is not positive.</exception>
    public long NextInt64(long exclusiveMaximum);

    /// <summary>Returns a value in <c>[0, 1)</c>.</summary>
    /// <returns>A uniformly distributed double.</returns>
    public double NextUnitDouble();

    /// <summary>Fills <paramref name="destination"/> with random bytes.</summary>
    /// <param name="destination">The span to fill; a zero-length span is valid.</param>
    public void Fill(Span<byte> destination);
}
