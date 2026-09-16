// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ToolResultPart behavior and contracts.</summary>
public sealed class ToolResultPartTests
{
    [Fact]
    public void ToolResultPart_WhenContentIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty), default, Projection(), ExtensionData.Empty));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ToolResultPart_WhenOutcomeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), null!, [], Projection(), ExtensionData.Empty));
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void ToolResultPart_WhenProjectionIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("projection");
    }

    [Fact]
    public void ToolResultPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = Tool();
        var outcome = Outcome();
        var projection = Projection();
        var content = ImmutableArray.Create<ContentPart>(new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty));
        var first = new ToolResultPart(callId, tool, outcome, content, projection, ExtensionData.Empty);
        var second = new ToolResultPart(callId, tool, outcome, content, projection, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ToolResultPart_Equality_WhenDifferentContent_InstancesAreNotEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = Tool();
        var outcome = Outcome();
        var first = new ToolResultPart(callId, tool, outcome, [new TextPart("a", TextSemantics.Plain, ExtensionData.Empty)], Projection(), ExtensionData.Empty);
        var second = new ToolResultPart(callId, tool, outcome, [new TextPart("b", TextSemantics.Plain, ExtensionData.Empty)], Projection(), ExtensionData.Empty);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void ToolResultPart_Equality_WhenDifferentProjection_InstancesAreNotEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = Tool();
        var outcome = Outcome();
        var content = ImmutableArray.Create<ContentPart>(new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty));
        var first = new ToolResultPart(callId, tool, outcome, content, Projection(), ExtensionData.Empty);
        var second = new ToolResultPart(
            callId,
            tool,
            outcome,
            content,
            new ToolResultProjectionInfo(
                new ToolResultProjectionPolicyReference(new ToolResultProjectionPolicyKey("other"), new ToolResultProjectionPolicyVersion(1)),
                [],
                0,
                0),
            ExtensionData.Empty);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void ToolResultPart_WhenContentContainsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [null!], Projection(), ExtensionData.Empty));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void With_WhenContentIsDefault_ThrowsArgumentException()
    {
        var part = new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], Projection(), ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => part with { Content = default });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenContentContainsNull_ThrowsArgumentException()
    {
        var part = new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], Projection(), ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => part with { Content = [null!] });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenToolIsNull_ThrowsArgumentNullException()
    {
        var part = new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], Projection(), ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => part with { Tool = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenOutcomeIsNull_ThrowsArgumentNullException()
    {
        var part = new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], Projection(), ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => part with { Outcome = null! });

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void With_WhenProjectionIsNull_ThrowsArgumentNullException()
    {
        var part = new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], Projection(), ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => part with { Projection = null! });

        exception.ParamName.ShouldBe("value");
    }

    private static ToolReference Tool() => new(new ToolAlias("t"), new ToolId("t"), new ToolVersion("1"));

    private static ToolCallOutcome Outcome() => new(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty);

    private static ToolResultProjectionInfo Projection() => new(
        ToolResultProjectionPolicyReference.Default,
        [],
        0,
        0);
}
