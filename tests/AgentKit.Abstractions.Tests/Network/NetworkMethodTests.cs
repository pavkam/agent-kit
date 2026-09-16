// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Network;

/// <summary>Verifies NetworkMethod behavior and contracts.</summary>
public sealed class NetworkMethodTests
{
    [Fact]
    public void Constructor_WhenValueIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new NetworkMethod(" ")).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_CanonicalizesValue()
    {
        var method = new NetworkMethod(" get ");
        method.Value.ShouldBe("GET");
    }

    [Fact]
    public void ToString_WhenCalled_ReturnsValue() =>
        NetworkMethod.Post.ToString().ShouldBe("POST");

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void StaticMembers_WhenAccessed_HaveExpectedValues(string expected)
    {
        var method = expected switch
        {
            "GET" => NetworkMethod.Get,
            "POST" => NetworkMethod.Post,
            "PUT" => NetworkMethod.Put,
            "PATCH" => NetworkMethod.Patch,
            "DELETE" => NetworkMethod.Delete,
            "HEAD" => NetworkMethod.Head,
            _ => NetworkMethod.Options,
        };
        method.Value.ShouldBe(expected);
    }
}
