// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ToolCallPresentationSource behavior and contracts.</summary>
public sealed class ToolCallPresentationSourceTests
{
    [Fact]
    public void Constructor_WhenCallIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolCallPresentationSource(null!)).ParamName.ShouldBe("call");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsCall()
    {
        var call = Call();
        var source = new ToolCallPresentationSource(call);
        source.Call.ShouldBeSameAs(call);
        ToolPresentationSource typed = source;
        _ = typed.ShouldBeOfType<ToolCallPresentationSource>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolCallPresentationSource(Call());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ToolCallPart Call() => new(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolAlias("read"), null, null), default, null, ExtensionData.Empty);
}
