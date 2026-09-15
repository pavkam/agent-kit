// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ToolReference behavior and contracts.</summary>
public sealed class ToolReferenceTests
{
    [Fact]
    public void ToolReference_Constructor_WhenNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new ToolReference(new ToolId("t"), null, " "));
    [Fact]
    public void ToolReference_Equality_WhenSameValues_InstancesAreEqual() => new ToolReference(new ToolId("t"), null, "tool").ShouldBe(new ToolReference(new ToolId("t"), null, "tool"));
    [Fact]
    public void ToolReference_WhenNameIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolReference(new ToolId("read"), null, "   "));
        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void With_WhenNameIsWhitespace_ThrowsArgumentException()
    {
        var reference = new ToolReference(new ToolId("t"), null, "tool");

        var exception = Should.Throw<ArgumentException>(() => reference with { Name = " " });

        exception.ParamName.ShouldBe("value");
    }
}
