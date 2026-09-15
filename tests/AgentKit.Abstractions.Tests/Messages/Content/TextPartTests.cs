// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies TextPart behavior and contracts.</summary>
public sealed class TextPartTests
{
    [Fact]
    public void TextPart_WhenTextIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TextPart(null!, TextSemantics.Plain, ExtensionData.Empty));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void TextPart_WhenArgumentsAreValid_ExposesValues()
    {
        var part = new TextPart("hi", TextSemantics.Markdown, ExtensionData.Empty);
        part.Text.ShouldBe("hi");
        part.Semantics.ShouldBe(TextSemantics.Markdown);
        part.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void TextPart_Equality_WhenSameValues_InstancesAreEqual() => new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty).ShouldBe(new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty));

    [Fact]
    public void With_WhenTextIsNull_ThrowsArgumentNullException()
    {
        var part = new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => part with { Text = null! });

        exception.ParamName.ShouldBe("value");
    }
}
