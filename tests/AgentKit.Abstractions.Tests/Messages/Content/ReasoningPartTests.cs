// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ReasoningPart behavior and contracts.</summary>
public sealed class ReasoningPartTests
{
    [Fact]
    public void ReasoningPart_WhenContentIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningPart(null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ReasoningPart_Constructor_WhenContentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningPart(null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void ReasoningPart_Equality_WhenSameValues_InstancesAreEqual()
    {
        var content = new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty);
        new ReasoningPart(content, ExtensionData.Empty).ShouldBe(new ReasoningPart(content, ExtensionData.Empty));
    }
}
