// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IRandomizerFactory"/> whose randomizers return one fixed unit value, so jitter and shuffles are exactly reproducible.</summary>
public sealed class FixedRandomizerFactory: IRandomizerFactory
{
    private readonly double _unit;

    /// <summary>Initializes the factory.</summary>
    /// <param name="unit">The value in [0, 1) every created randomizer returns from <see cref="IRandomizer.NextUnitDouble"/>.</param>
    public FixedRandomizerFactory(double unit = 0.0) => _unit = unit;

    /// <summary>Gets the number of randomizers created, so a test can prove the factory is consulted only when needed.</summary>
    public int Created { get; private set; }

    /// <inheritdoc/>
    public IRandomizer Create(RandomizerCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Created++;
        return new FixedRandomizer(_unit);
    }

    private sealed class FixedRandomizer(double unit): IRandomizer
    {
        public RandomizerDescriptor Descriptor { get; } = new(
            new RandomizerAlgorithmId("test.fixed"),
            new RandomizerAlgorithmVersion("1"),
            isDeterministic: true,
            new ContentHash("sha256:fixed"));

        public int NextInt32(int exclusiveMaximum) => 0;

        public long NextInt64(long exclusiveMaximum) => 0;

        public double NextUnitDouble() => unit;

        public void Fill(Span<byte> destination) => destination.Clear();
    }
}
