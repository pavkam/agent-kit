// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ToolResultPresentationSource behavior and contracts.</summary>
public sealed class ToolResultPresentationSourceTests
{
    [Fact]
    public void Constructor_WhenResultIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolResultPresentationSource(null!)).ParamName.ShouldBe("result");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsResult()
    {
        var result = Result();
        var source = new ToolResultPresentationSource(result);
        source.Result.ShouldBeSameAs(result);
        ToolPresentationSource typed = source;
        _ = typed.ShouldBeOfType<ToolResultPresentationSource>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolResultPresentationSource(Result());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ToolReference Tool() => new(new ToolAlias("t"), new ToolId("t"), new ToolVersion("1"));

    private static ToolCallOutcome Outcome() => new(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty);

    private static ToolResultProjectionInfo Projection() => new(
        ToolResultProjectionPolicyReference.Default,
        [],
        0,
        0);

    private static ToolResultPart Result() => new(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], Projection(), ExtensionData.Empty);
}
