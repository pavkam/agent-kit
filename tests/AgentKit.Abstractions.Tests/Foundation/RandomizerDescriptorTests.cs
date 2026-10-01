// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

/// <summary>Verifies RandomizerDescriptor behavior and contracts.</summary>
public sealed class RandomizerDescriptorTests
{
    private static readonly RandomizerAlgorithmId _algorithm = new("algo");
    private static readonly RandomizerAlgorithmVersion _version = new("1");

    [Fact]
    public void Constructor_WhenNondeterministicWithoutSeed_PreservesFields()
    {
        var descriptor = new RandomizerDescriptor(_algorithm, _version, isDeterministic: false, seedFingerprint: null);

        descriptor.Algorithm.ShouldBe(_algorithm);
        descriptor.AlgorithmVersion.ShouldBe(_version);
        descriptor.IsDeterministic.ShouldBeFalse();
        descriptor.SeedFingerprint.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenDeterministicWithSeedFingerprint_PreservesIt()
    {
        var seed = new ContentHash("sha256:seed");

        var descriptor = new RandomizerDescriptor(_algorithm, _version, isDeterministic: true, seed);

        descriptor.IsDeterministic.ShouldBeTrue();
        descriptor.SeedFingerprint.ShouldBe(seed);
    }

    [Fact]
    public void Constructor_WhenAlgorithmIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new RandomizerDescriptor(default, _version, false, null)).ParamName.ShouldBe("algorithm");

    [Fact]
    public void Constructor_WhenAlgorithmVersionIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new RandomizerDescriptor(_algorithm, default, false, null)).ParamName.ShouldBe("algorithmVersion");

    [Fact]
    public void Constructor_WhenDeterministicWithoutSeed_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new RandomizerDescriptor(_algorithm, _version, true, null)).ParamName.ShouldBe("seedFingerprint");

    [Fact]
    public void Constructor_WhenNondeterministicWithSeed_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new RandomizerDescriptor(_algorithm, _version, false, new ContentHash("sha256:seed")))
            .ParamName.ShouldBe("seedFingerprint");
}
