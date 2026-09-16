// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ToolPresentationRequest behavior and contracts.</summary>
public sealed class ToolPresentationRequestTests
{
    [Fact]
    public void Constructor_WhenSourceIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolPresentationRequest(null, null!, new ToolPresentationBounds())).ParamName.ShouldBe("source");

    [Fact]
    public void Constructor_WhenBoundsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolPresentationRequest(null, Source(), null!)).ParamName.ShouldBe("bounds");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var source = Source();
        var bounds = new ToolPresentationBounds();
        var request = new ToolPresentationRequest(null, source, bounds);
        request.Descriptor.ShouldBeNull();
        request.Source.ShouldBeSameAs(source);
        request.Bounds.ShouldBeSameAs(bounds);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolPresentationRequest(null, Source(), new ToolPresentationBounds());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ToolCallPresentationSource Source() => new(new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("read"), null, null), default, null, ExtensionData.Empty));
}
