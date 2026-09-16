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

    [Fact]
    public void ToolResultTextContent_Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var content = new ToolResultTextContent("text", TextSemantics.Plain, ExtensionData.Empty);
        content.Text.ShouldBe("text");
        content.Semantics.ShouldBe(TextSemantics.Plain);
        content.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolResultTextContent("text", TextSemantics.Plain, ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
