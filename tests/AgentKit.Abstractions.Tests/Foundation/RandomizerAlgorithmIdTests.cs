// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

/// <summary>Verifies RandomizerAlgorithmId behavior and contracts.</summary>
public sealed class RandomizerAlgorithmIdTests
{
    [Fact]
    public void Constructor_WhenValueIsValid_PreservesIt()
    {
        var value = new RandomizerAlgorithmId("value");

        value.Value.ShouldBe("value");
        value.ToString().ShouldBe("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactParameter(string? value) =>
        Should.Throw<ArgumentException>(() => new RandomizerAlgorithmId(value!)).ParamName.ShouldBe("value");

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmpty() =>
        default(RandomizerAlgorithmId).ToString().ShouldBeEmpty();

    [Fact]
    public void Equality_WhenValuesMatch_IsEqual() =>
        new RandomizerAlgorithmId("same").ShouldBe(new RandomizerAlgorithmId("same"));
}
