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
        var exception = Should.Throw<ArgumentException>(() => new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolId("read"), null, "read"), new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty), default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ToolResultPart_WhenOutcomeIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolId("read"), null, "read"), null!, [], ExtensionData.Empty));
        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void ToolResultPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("t"), null, "tool");
        var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty);
        var content = ImmutableArray.Create<ContentPart>(new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty));
        var first = new ToolResultPart(callId, tool, outcome, content, ExtensionData.Empty);
        var second = new ToolResultPart(callId, tool, outcome, content, ExtensionData.Empty);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void ToolResultPart_Equality_WhenDifferentContent_InstancesAreNotEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("t"), null, "tool");
        var outcome = new ToolCallOutcome(ToolCallOutcomeKind.Success, null, ExtensionData.Empty);
        var first = new ToolResultPart(callId, tool, outcome, [new TextPart("a", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        var second = new ToolResultPart(callId, tool, outcome, [new TextPart("b", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);
        first.ShouldNotBe(second);
    }
}
