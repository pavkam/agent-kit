// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies DelegationScope constraints and value semantics.</summary>
public sealed class DelegationScopeTests
{
    [Fact]
    public void Constructor_WhenToolsAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DelegationScope(default, [])).ParamName.ShouldBe("allowedTools");

    [Fact]
    public void Constructor_WhenDataScopesAreDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DelegationScope([], default)).ParamName.ShouldBe("dataScopes");

    [Fact]
    public void Constructor_WhenAToolIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DelegationScope([default], [])).ParamName.ShouldBe("allowedTools");

    [Fact]
    public void Constructor_WhenADataScopeIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DelegationScope([], [" "])).ParamName.ShouldBe("dataScopes");

    [Fact]
    public void Equality_WhenMembersMatchByContent_IsStructural() =>
        new DelegationScope([new ToolId("read")], ["d"]).ShouldBe(new DelegationScope([new ToolId("read")], ["d"]));
}
