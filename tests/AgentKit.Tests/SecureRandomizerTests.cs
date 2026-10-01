// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.Internal;

/// <summary>Verifies <see cref="SecureRandomizer"/> behavior.</summary>
public sealed class SecureRandomizerTests
{
    [Fact]
    public void Descriptor_WhenRead_ReportsTheSystemCsprngAsNondeterministic()
    {
        var descriptor = new SecureRandomizer().Descriptor;

        descriptor.Algorithm.ShouldBe(new RandomizerAlgorithmId("agentkit.system-csprng"));
        descriptor.IsDeterministic.ShouldBeFalse();
        descriptor.SeedFingerprint.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NextInt32_WhenMaximumIsNotPositive_ThrowsExactParameter(int maximum) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecureRandomizer().NextInt32(maximum)).ParamName.ShouldBe("exclusiveMaximum");

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void NextInt64_WhenMaximumIsNotPositive_ThrowsExactParameter(long maximum) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SecureRandomizer().NextInt64(maximum)).ParamName.ShouldBe("exclusiveMaximum");

    [Fact]
    public void NextInt32_WhenMaximumIsOne_AlwaysReturnsZero()
    {
        var randomizer = new SecureRandomizer();

        Enumerable.Range(0, 50).Select(_ => randomizer.NextInt32(1)).ShouldAllBe(static value => value == 0);
    }

    [Fact]
    public void NextInt32_WhenSampledRepeatedly_StaysInsideTheBoundAndCoversIt()
    {
        var randomizer = new SecureRandomizer();

        var values = Enumerable.Range(0, 2_000).Select(_ => randomizer.NextInt32(7)).ToArray();

        values.ShouldAllBe(static value => value >= 0 && value < 7);
        values.Distinct().Count().ShouldBe(7);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(3L)]
    [InlineData(long.MaxValue)]
    public void NextInt64_WhenSampled_StaysInsideTheBound(long maximum)
    {
        var randomizer = new SecureRandomizer();

        Enumerable.Range(0, 500).Select(_ => randomizer.NextInt64(maximum)).ShouldAllBe(value => value >= 0 && value < maximum);
    }

    [Fact]
    public void NextUnitDouble_WhenSampled_StaysInTheHalfOpenUnitInterval()
    {
        var randomizer = new SecureRandomizer();

        var values = Enumerable.Range(0, 1_000).Select(_ => randomizer.NextUnitDouble()).ToArray();

        values.ShouldAllBe(static value => value >= 0d && value < 1d);
        values.Distinct().Count().ShouldBeGreaterThan(900);
    }

    [Fact]
    public void Fill_WhenDestinationIsEmpty_DoesNothing() =>
        Should.NotThrow(() => new SecureRandomizer().Fill([]));

    [Fact]
    public void Fill_WhenDestinationIsLarge_WritesNonConstantBytes()
    {
        var bytes = new byte[64];

        new SecureRandomizer().Fill(bytes);

        bytes.Distinct().Count().ShouldBeGreaterThan(1);
    }
}
