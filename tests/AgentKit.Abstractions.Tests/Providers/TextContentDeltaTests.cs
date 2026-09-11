// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies TextContentDelta behavior and contracts.</summary>
public sealed class TextContentDeltaTests
{
    [Fact]
    public void TextContentDelta_Constructor_WhenTextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TextContentDelta(null!));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void TextContentDelta_Constructor_WhenTextEmpty_DoesNotThrow()
    {
        var delta = new TextContentDelta(string.Empty);
        delta.Text.ShouldBe(string.Empty);
    }

    [Fact]
    public void TextContentDelta_Constructor_WhenValid_RoundTripsText()
    {
        var delta = new TextContentDelta("chunk");
        delta.Text.ShouldBe("chunk");
    }

    [Fact]
    public void TextContentDelta_Equality_WhenSameText_InstancesAreEqual() => new TextContentDelta("chunk").ShouldBe(new TextContentDelta("chunk"));
}
