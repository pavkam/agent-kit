// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes how an <see cref="IRandomizer"/> produces values, for replay and diagnostics.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality. It never carries raw seed material: a
/// deterministic randomizer reports only <see cref="SeedFingerprint"/>.
/// </remarks>
public sealed record RandomizerDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="algorithm">The algorithm identity.</param>
    /// <param name="algorithmVersion">The algorithm version.</param>
    /// <param name="isDeterministic">Whether the same seed, operation, and purpose reproduce the same stream.</param>
    /// <param name="seedFingerprint">The fingerprint of the configured seed for a deterministic randomizer, otherwise <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="algorithm"/> or <paramref name="algorithmVersion"/> is the default value, a deterministic
    /// randomizer has no <paramref name="seedFingerprint"/>, or a nondeterministic one reports one.
    /// </exception>
    public RandomizerDescriptor(
        RandomizerAlgorithmId algorithm,
        RandomizerAlgorithmVersion algorithmVersion,
        bool isDeterministic,
        ContentHash? seedFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm.Value, nameof(algorithm));
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithmVersion.Value, nameof(algorithmVersion));
        if (isDeterministic != seedFingerprint.HasValue)
        {
            throw new ArgumentException(
                "A deterministic randomizer reports exactly its seed fingerprint; a nondeterministic one reports none.",
                nameof(seedFingerprint));
        }

        Algorithm = algorithm;
        AlgorithmVersion = algorithmVersion;
        IsDeterministic = isDeterministic;
        SeedFingerprint = seedFingerprint;
    }

    /// <summary>Gets the algorithm identity.</summary>
    public RandomizerAlgorithmId Algorithm { get; }

    /// <summary>Gets the algorithm version.</summary>
    public RandomizerAlgorithmVersion AlgorithmVersion { get; }

    /// <summary>Gets whether the stream is reproducible from its seed.</summary>
    public bool IsDeterministic { get; }

    /// <summary>Gets the seed fingerprint of a deterministic randomizer.</summary>
    /// <value>The fingerprint, or <see langword="null"/> for a nondeterministic randomizer.</value>
    public ContentHash? SeedFingerprint { get; }
}
