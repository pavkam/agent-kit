// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using System.Text.Json;

using AgentKit;

/// <summary>Verifies UnknownContentPart behavior and contracts.</summary>
public sealed class UnknownContentPartTests
{
    [Fact]
    public void UnknownContentPart_WhenTypeNameIsWhitespace_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new UnknownContentPart("   ", default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("typeName");
    }

    [Fact]
    public void UnknownContentPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var raw = JsonDocument.Parse("{}").RootElement;
        var part = new UnknownContentPart("custom", raw, ExtensionData.Empty);
        var other = new UnknownContentPart("custom", raw, ExtensionData.Empty);
        part.ShouldBe(other);
        part.GetHashCode().ShouldBe(other.GetHashCode());
    }

    [Fact]
    public void With_WhenTypeNameIsWhitespace_ThrowsArgumentException()
    {
        var part = new UnknownContentPart("vendor.kind", default, ExtensionData.Empty);

        var exception = Should.Throw<ArgumentException>(() => part with { TypeName = " " });

        exception.ParamName.ShouldBe("value");
    }
}
