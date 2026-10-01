// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

/// <summary>Verifies ContentHashAlgorithmVersion behavior and contracts.</summary>
public sealed class ContentHashAlgorithmVersionTests
{
    [Fact]
    public void Constructor_WhenValueIsValid_PreservesIt()
    {
        var value = new ContentHashAlgorithmVersion("value");

        value.Value.ShouldBe("value");
        value.ToString().ShouldBe("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactParameter(string? value) =>
        Should.Throw<ArgumentException>(() => new ContentHashAlgorithmVersion(value!)).ParamName.ShouldBe("value");

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmpty() =>
        default(ContentHashAlgorithmVersion).ToString().ShouldBeEmpty();

    [Fact]
    public void Equality_WhenValuesMatch_IsEqual() =>
        new ContentHashAlgorithmVersion("same").ShouldBe(new ContentHashAlgorithmVersion("same"));
}
