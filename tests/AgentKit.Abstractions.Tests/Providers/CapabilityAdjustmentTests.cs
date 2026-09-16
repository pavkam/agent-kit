// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies CapabilityAdjustment behavior and contracts.</summary>
public sealed class CapabilityAdjustmentTests
{
    [Fact]
    public void Constructor_WhenCapabilityIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new CapabilityAdjustment((ModelCapabilityKind) 99, "description")).ParamName.ShouldBe("capability");

    [Fact]
    public void Constructor_WhenDescriptionIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new CapabilityAdjustment(ModelCapabilityKind.Streaming, " ")).ParamName.ShouldBe("description");

    [Fact]
    public void Initializer_WhenCapabilityIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var adjustment = Adjustment();
        Should.Throw<ArgumentOutOfRangeException>(() => adjustment with { Capability = (ModelCapabilityKind) 99 }).ParamName.ShouldBe("Capability");
    }

    [Fact]
    public void Initializer_WhenDescriptionIsBlank_ThrowsExactArgumentException()
    {
        var adjustment = Adjustment();
        Should.Throw<ArgumentException>(() => adjustment with { Description = " " }).ParamName.ShouldBe("Description");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var adjustment = Adjustment();
        adjustment.Capability.ShouldBe(ModelCapabilityKind.Streaming);
        adjustment.Description.ShouldBe("streaming disabled");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Adjustment();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static CapabilityAdjustment Adjustment() => new(ModelCapabilityKind.Streaming, "streaming disabled");
}
