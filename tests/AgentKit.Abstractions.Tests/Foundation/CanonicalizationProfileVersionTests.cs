// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Foundation;

/// <summary>Verifies CanonicalizationProfileVersion behavior and contracts.</summary>
public sealed class CanonicalizationProfileVersionTests
{
    [Fact]
    public void Constructor_WhenValueIsPositive_PreservesIt()
    {
        var version = new CanonicalizationProfileVersion(3);

        version.Value.ShouldBe(3);
        version.ToString().ShouldBe("3");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenValueIsNotPositive_ThrowsExactParameter(long value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new CanonicalizationProfileVersion(value)).ParamName.ShouldBe("value");

    [Fact]
    public void Equality_WhenDefault_IsNotEqualToAValidVersion() =>
        default(CanonicalizationProfileVersion).ShouldNotBe(new CanonicalizationProfileVersion(1));
}
