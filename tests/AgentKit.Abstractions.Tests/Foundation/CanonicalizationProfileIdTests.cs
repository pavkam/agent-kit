// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

/// <summary>Verifies CanonicalizationProfileId behavior and contracts.</summary>
public sealed class CanonicalizationProfileIdTests
{
    [Fact]
    public void Constructor_WhenValueIsValid_PreservesIt()
    {
        var value = new CanonicalizationProfileId("value");

        value.Value.ShouldBe("value");
        value.ToString().ShouldBe("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactParameter(string? value) =>
        Should.Throw<ArgumentException>(() => new CanonicalizationProfileId(value!)).ParamName.ShouldBe("value");

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmpty() =>
        default(CanonicalizationProfileId).ToString().ShouldBeEmpty();

    [Fact]
    public void Equality_WhenValuesMatch_IsEqual() =>
        new CanonicalizationProfileId("same").ShouldBe(new CanonicalizationProfileId("same"));
}
