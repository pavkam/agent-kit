// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ContentPart behavior and contracts.</summary>
public sealed class ContentPartTests
{
    [Fact]
    public void AnyContentPart_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TextPart("hi", TextSemantics.Plain, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void With_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var part = new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentNullException>(() => part with { Extensions = null! });

        exception.ParamName.ShouldBe("value");
    }
}
