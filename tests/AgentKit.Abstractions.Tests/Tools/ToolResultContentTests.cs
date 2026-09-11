// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultContent behavior and contracts.</summary>
public sealed class ToolResultContentTests
{
    [Fact]
    public void ToolResultContent_WhenEnumerated_IsClosedToCanonicalVariants()
    {
        var variants = typeof(ToolResultContent).Assembly.GetTypes().Where(type => type.BaseType == typeof(ToolResultContent)).Select(type => type.Name).Order(StringComparer.Ordinal).ToArray();
        variants.ShouldBe([nameof(ToolResultArtifactContent), nameof(ToolResultMediaContent), nameof(ToolResultOpaqueContent), nameof(ToolResultStructuredContent), nameof(ToolResultTextContent),]);
    }

    [Fact]
    public void ToolResultContent_CopyConstructor_WhenExternalVariantBootstrapsFromBuiltIn_ThrowsExactException()
    {
        var original = new ToolResultTextContent("text", TextSemantics.Plain, ExtensionData.Empty);
        var exception = Should.Throw<ArgumentException>(() => new ForeignToolResultContent(original));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void ToolResultContent_CopyConstructor_WhenOriginalNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignToolResultContent(null!));
        exception.ParamName.ShouldBe("original");
    }

    [Fact]
    public void ToolResultContent_CopyConstructor_WhenBuiltInVariantCopies_PreservesValue()
    {
        var original = new ToolResultTextContent("text", TextSemantics.Plain, ExtensionData.Empty);
        var copy = original with
        {
        };
        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
        copy.GetType().ShouldBe(typeof(ToolResultTextContent));
    }

    private sealed record ForeignToolResultContent(ToolResultContent Original): ToolResultContent(Original);
}
