// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using AgentKit;

/// <summary>Verifies ReasoningContent behavior and contracts.</summary>
public sealed class ReasoningContentTests
{
    [Fact]
    public void ReasoningContent_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningContent(null, ReasoningVisibility.Visible, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ReasoningContent_Equality_WhenSameValues_InstancesAreEqual() => new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty).ShouldBe(new ReasoningContent("thinking", ReasoningVisibility.Visible, null, ExtensionData.Empty));
    [Fact]
    public void ReasoningContent_WhenExtensionsIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningContent("thinking", ReasoningVisibility.Visible, null, null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ReasoningContent_WhenVisibilityIsRedacted_TextMayBeNull()
    {
        var content = new ReasoningContent(null, ReasoningVisibility.Redacted, null, ExtensionData.Empty);
        content.Text.ShouldBeNull();
        content.Visibility.ShouldBe(ReasoningVisibility.Redacted);
    }
}
