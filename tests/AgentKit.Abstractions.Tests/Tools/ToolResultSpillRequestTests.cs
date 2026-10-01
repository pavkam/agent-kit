// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies <see cref="ToolResultSpillRequest"/> validates and retains its call and content.</summary>
public sealed class ToolResultSpillRequestTests
{
    [Fact]
    public void Constructor_WhenValid_RetainsTheCallAndContent()
    {
        var call = ToolRuntimeTestFixture.ValidatedCall();

        var request = new ToolResultSpillRequest(call, [1, 2, 3]);

        request.Call.ShouldBeSameAs(call);
        request.Content.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Constructor_WhenTheCallIsNull_ThrowsNamingIt() =>
        Should.Throw<ArgumentNullException>(() => new ToolResultSpillRequest(null!, [1])).ParamName.ShouldBe("call");

    [Fact]
    public void Constructor_WhenTheContentIsUninitializedOrEmpty_ThrowsNamingIt()
    {
        var call = ToolRuntimeTestFixture.ValidatedCall();

        Should.Throw<ArgumentException>(() => new ToolResultSpillRequest(call, default)).ParamName.ShouldBe("content");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultSpillRequest(call, [])).ParamName.ShouldBe("content");
    }
}
