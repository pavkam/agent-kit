// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies CapabilityValidationResult derived behavior and contracts.</summary>
public sealed class CapabilityValidationResultTests
{
    [Fact]
    public void CapabilitiesSupported_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new CapabilitiesSupported();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void CapabilitiesDowngraded_WhenAdjustmentsAreDefaultOrEmpty_ThrowsExactArgumentException()
    {
        Should.Throw<ArgumentException>(() => new CapabilitiesDowngraded(default)).ParamName.ShouldBe("adjustments");
        Should.Throw<ArgumentException>(() => new CapabilitiesDowngraded([])).ParamName.ShouldBe("adjustments");
    }

    [Fact]
    public void CapabilitiesDowngraded_WhenAdjustmentsContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new CapabilitiesDowngraded([null!])).ParamName.ShouldBe("adjustments");

    [Fact]
    public void CapabilitiesDowngraded_Initializer_WhenAdjustmentsAreInvalid_ThrowsExactArgumentException()
    {
        var downgraded = Downgraded();
        Should.Throw<ArgumentException>(() => downgraded with { Adjustments = default }).ParamName.ShouldBe("Adjustments");
        Should.Throw<ArgumentException>(() => downgraded with { Adjustments = [null!] }).ParamName.ShouldBe("Adjustments");
    }

    [Fact]
    public void CapabilitiesDowngraded_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var downgraded = Downgraded();
        downgraded.Adjustments.ShouldBe([Adjustment()]);
    }

    [Fact]
    public void CapabilitiesDowngraded_With_WhenApplied_ProducesEqualCopy()
    {
        var original = Downgraded();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void CapabilitiesDowngraded_Initializer_WhenAdjustmentsAreValid_ReplacesValue()
    {
        var original = Downgraded();
        var replacement = new CapabilityAdjustment(ModelCapabilityKind.ParallelToolCalls, "parallel calls disabled");
        var copy = original with { Adjustments = [replacement] };
        copy.Adjustments.ShouldBe([replacement]);
    }

    [Fact]
    public void CapabilitiesUnsupported_WhenCapabilitiesAreDefaultOrEmpty_ThrowsExactArgumentException()
    {
        Should.Throw<ArgumentException>(() => new CapabilitiesUnsupported(default)).ParamName.ShouldBe("capabilities");
        Should.Throw<ArgumentException>(() => new CapabilitiesUnsupported([])).ParamName.ShouldBe("capabilities");
    }

    [Fact]
    public void CapabilitiesUnsupported_WhenCapabilitiesContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new CapabilitiesUnsupported([null!])).ParamName.ShouldBe("capabilities");

    [Fact]
    public void CapabilitiesUnsupported_Initializer_WhenCapabilitiesAreInvalid_ThrowsExactArgumentException()
    {
        var unsupported = Unsupported();
        Should.Throw<ArgumentException>(() => unsupported with { Capabilities = default }).ParamName.ShouldBe("Capabilities");
        Should.Throw<ArgumentException>(() => unsupported with { Capabilities = [null!] }).ParamName.ShouldBe("Capabilities");
    }

    [Fact]
    public void CapabilitiesUnsupported_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var unsupported = Unsupported();
        unsupported.Capabilities.ShouldBe([UnsupportedItem()]);
    }

    [Fact]
    public void CapabilitiesUnsupported_With_WhenApplied_ProducesEqualCopy()
    {
        var original = Unsupported();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void CapabilitiesUnsupported_Initializer_WhenCapabilitiesAreValid_ReplacesValue()
    {
        var original = Unsupported();
        var replacement = new UnsupportedCapability(ModelCapabilityKind.Streaming, "streaming not supported");
        var copy = original with { Capabilities = [replacement] };
        copy.Capabilities.ShouldBe([replacement]);
    }

    private static CapabilityAdjustment Adjustment() => new(ModelCapabilityKind.Streaming, "streaming disabled");
    private static CapabilitiesDowngraded Downgraded() => new([Adjustment()]);
    private static UnsupportedCapability UnsupportedItem() => new(ModelCapabilityKind.ParallelToolCalls, "not supported");
    private static CapabilitiesUnsupported Unsupported() => new([UnsupportedItem()]);
}
