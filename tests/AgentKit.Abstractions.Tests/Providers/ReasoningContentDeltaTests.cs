// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ReasoningContentDelta behavior and contracts.</summary>
public sealed class ReasoningContentDeltaTests
{
    [Fact]
    public void ReasoningContentDelta_Constructor_WhenTextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningContentDelta(null!, ExtensionData.Empty));
        exception.ParamName.ShouldBe("text");
    }

    [Fact]
    public void ReasoningContentDelta_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ReasoningContentDelta("thinking", null!));
        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ReasoningContentDelta_Constructor_WhenValid_RoundTripsProperties()
    {
        var delta = new ReasoningContentDelta("thinking", ExtensionData.Empty);
        delta.Text.ShouldBe("thinking");
        delta.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void ReasoningContentDelta_Equality_WhenSameValues_InstancesAreEqual() => new ReasoningContentDelta("thinking", ExtensionData.Empty).ShouldBe(new ReasoningContentDelta("thinking", ExtensionData.Empty));
}
