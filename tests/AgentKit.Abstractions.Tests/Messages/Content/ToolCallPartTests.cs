// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ToolCallPart behavior and contracts.</summary>
public sealed class ToolCallPartTests
{
    [Fact]
    public void ToolCallPart_WhenToolIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolCallPart(new ToolCallId(Guid.NewGuid()), null!, default, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("tool");
    }

    [Fact]
    public void ToolCallPart_WhenArgumentsAreValid_ExposesValues()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("read"), null, "read");
        var part = new ToolCallPart(callId, tool, default, null, ExtensionData.Empty);
        part.CallId.ShouldBe(callId);
        part.Tool.ShouldBe(tool);
        part.ProviderCallId.ShouldBeNull();
    }

    [Fact]
    public void ToolCallPart_Constructor_WhenToolNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolCallPart(new ToolCallId(Guid.NewGuid()), null!, default, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("tool");
    }

    [Fact]
    public void ToolCallPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("t"), null, "tool");
        new ToolCallPart(callId, tool, default, null, ExtensionData.Empty).ShouldBe(new ToolCallPart(callId, tool, default, null, ExtensionData.Empty));
    }
}
