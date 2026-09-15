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

    [Fact]
    public void ToolCallPart_Equality_WhenArgumentsAreStructurallyEqualButParsedSeparately_InstancesAreEqual()
    {
        // Messages are immutable values; a part rebuilt from persisted JSON must equal its original so idempotent
        // replay, fingerprinting, and dedup do not depend on JsonElement backing-document identity.
        var callId = new ToolCallId(Guid.NewGuid());
        var tool = new ToolReference(new ToolId("t"), null, "tool");
        using var first = System.Text.Json.JsonDocument.Parse("""{"path":"a.txt","limit":10}""");
        using var second = System.Text.Json.JsonDocument.Parse("""{"path":"a.txt","limit":10}""");

        var left = new ToolCallPart(callId, tool, first.RootElement.Clone(), null, ExtensionData.Empty);
        var right = new ToolCallPart(callId, tool, second.RootElement.Clone(), null, ExtensionData.Empty);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void With_WhenToolIsNull_ThrowsArgumentNullException()
    {
        var part = new ToolCallPart(new ToolCallId(Guid.NewGuid()), new ToolReference(new ToolId("read"), null, "read"), default, null, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => part with { Tool = null! });

        exception.ParamName.ShouldBe("value");
    }
}
