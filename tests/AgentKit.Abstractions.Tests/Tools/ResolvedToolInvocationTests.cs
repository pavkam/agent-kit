// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ResolvedToolInvocation behavior and contracts.</summary>
public sealed class ResolvedToolInvocationTests
{
    [Fact]
    public void Constructor_WhenToolIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ResolvedToolInvocation(null!, ProjectionPolicy(), Invocation())).ParamName.ShouldBe("tool");

    [Fact]
    public void Constructor_WhenProjectionPolicyIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ResolvedToolInvocation(Tool(), null!, Invocation())).ParamName.ShouldBe("projectionPolicy");

    [Fact]
    public void Constructor_WhenInvocationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ResolvedToolInvocation(Tool(), ProjectionPolicy(), null!)).ParamName.ShouldBe("invocation");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var tool = Tool();
        var projectionPolicy = ProjectionPolicy();
        var invocation = Invocation();
        var resolved = new ResolvedToolInvocation(tool, projectionPolicy, invocation);
        resolved.Tool.ShouldBe(tool);
        resolved.ProjectionPolicy.ShouldBe(projectionPolicy);
        resolved.Invocation.ShouldBe(invocation);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ResolvedToolInvocation(Tool(), ProjectionPolicy(), Invocation());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ToolReference Tool() => new(new ToolAlias("read"), new ToolId("read"), new ToolVersion("1"));

    private static ToolResultProjectionPolicyReference ProjectionPolicy() => new(new ToolResultProjectionPolicyKey("projection"), new ToolResultProjectionPolicyVersion(1));

    private static ToolInvocationResult Invocation() => new(
        new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty),
        []);
}
