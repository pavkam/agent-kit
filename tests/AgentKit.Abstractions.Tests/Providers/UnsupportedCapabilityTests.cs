// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies UnsupportedCapability behavior and contracts.</summary>
public sealed class UnsupportedCapabilityTests
{
    [Fact]
    public void Constructor_WhenCapabilityIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new UnsupportedCapability((ModelCapabilityKind) 99, "reason")).ParamName.ShouldBe("capability");

    [Fact]
    public void Constructor_WhenReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new UnsupportedCapability(ModelCapabilityKind.ToolCalls, " ")).ParamName.ShouldBe("reason");

    [Fact]
    public void Initializer_WhenCapabilityIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var capability = Capability();
        Should.Throw<ArgumentOutOfRangeException>(() => capability with { Capability = (ModelCapabilityKind) 99 }).ParamName.ShouldBe("Capability");
    }

    [Fact]
    public void Initializer_WhenReasonIsBlank_ThrowsExactArgumentException()
    {
        var capability = Capability();
        Should.Throw<ArgumentException>(() => capability with { Reason = " " }).ParamName.ShouldBe("Reason");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var capability = Capability();
        capability.Capability.ShouldBe(ModelCapabilityKind.ToolCalls);
        capability.Reason.ShouldBe("not supported");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Capability();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Initializer_WhenReasonIsValid_ReplacesValue()
    {
        var original = Capability();
        var copy = original with { Reason = "updated" };
        copy.Reason.ShouldBe("updated");
    }

    private static UnsupportedCapability Capability() => new(ModelCapabilityKind.ToolCalls, "not supported");
}
