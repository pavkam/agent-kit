// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

/// <summary>Verifies RandomizerPurpose behavior and contracts.</summary>
public sealed class RandomizerPurposeTests
{
    [Fact]
    public void Constructor_WhenValueIsValid_PreservesIt()
    {
        var value = new RandomizerPurpose("value");

        value.Value.ShouldBe("value");
        value.ToString().ShouldBe("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactParameter(string? value) =>
        Should.Throw<ArgumentException>(() => new RandomizerPurpose(value!)).ParamName.ShouldBe("value");

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmpty() =>
        default(RandomizerPurpose).ToString().ShouldBeEmpty();

    [Fact]
    public void Equality_WhenValuesMatch_IsEqual() =>
        new RandomizerPurpose("same").ShouldBe(new RandomizerPurpose("same"));
}
