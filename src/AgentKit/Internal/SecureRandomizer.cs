// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

using System.Security.Cryptography;

/// <summary>A cryptographically strong <see cref="IRandomizer"/> over <see cref="RandomNumberGenerator"/>.</summary>
/// <remarks>Reports a nondeterministic descriptor. The type holds no state, but the contract still forbids sharing one instance across parallel branches.</remarks>
internal sealed class SecureRandomizer: IRandomizer
{
    private static readonly RandomizerDescriptor _descriptor = new(
        new RandomizerAlgorithmId("agentkit.system-csprng"),
        new RandomizerAlgorithmVersion("1"),
        isDeterministic: false,
        seedFingerprint: null);

    /// <inheritdoc/>
    public RandomizerDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exclusiveMaximum"/> is not positive.</exception>
    public int NextInt32(int exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
        return RandomNumberGenerator.GetInt32(exclusiveMaximum);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exclusiveMaximum"/> is not positive.</exception>
    public long NextInt64(long exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
        // Rejection sampling over the largest multiple of the bound keeps the distribution unbiased: the
        // accepted range [0, limit] holds exactly 2^64 - (2^64 mod bound) values.
        var bound = (ulong) exclusiveMaximum;
        var limit = ulong.MaxValue - (((ulong.MaxValue % bound) + 1) % bound);
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        while (true)
        {
            RandomNumberGenerator.Fill(bytes);
            var candidate = BitConverter.ToUInt64(bytes);
            if (candidate <= limit)
            {
                return (long) (candidate % bound);
            }
        }
    }

    /// <inheritdoc/>
    public double NextUnitDouble()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        // The top 53 bits fill a double mantissa exactly, yielding a uniform value in [0, 1).
        return (BitConverter.ToUInt64(bytes) >> 11) * (1.0 / (1UL << 53));
    }

    /// <inheritdoc/>
    public void Fill(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
