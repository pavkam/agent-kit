// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultTextContent behavior and contracts.</summary>
public sealed class ToolResultTextContentTests
{
    [Fact]
    public void ToolResultTextContent_Constructor_WhenSemanticsUndefined_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultTextContent("text", (TextSemantics) 99, ExtensionData.Empty));
        exception.ParamName.ShouldBe("semantics");
    }

    [Fact]
    public void ToolResultTextContent_Constructor_WhenTextNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultTextContent(null!, TextSemantics.Plain, ExtensionData.Empty));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void ToolResultTextContent_Constructor_WhenExtensionsNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultTextContent("text", TextSemantics.Plain, null!));
        exception.ParamName.ShouldBe("extensions");
    }
}
