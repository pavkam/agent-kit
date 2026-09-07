// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

public sealed class ContentDeltaHierarchyTests
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
    public void TextContentDelta_Equality_WhenSameText_InstancesAreEqual() =>
        new TextContentDelta("chunk").ShouldBe(new TextContentDelta("chunk"));

    [Fact]
    public void StructuredDataContentDelta_Constructor_WhenFragmentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new StructuredDataContentDelta(null!));

        exception.ParamName.ShouldBe("jsonFragment");
    }

    [Fact]
    public void StructuredDataContentDelta_Constructor_WhenValid_RoundTripsFragment()
    {
        var delta = new StructuredDataContentDelta(/*lang=json,strict*/ """{"a":1}""");

        delta.JsonFragment.ShouldBe(/*lang=json,strict*/ """{"a":1}""");
    }

    [Fact]
    public void StructuredDataContentDelta_Equality_WhenSameFragment_InstancesAreEqual() =>
        new StructuredDataContentDelta("{}").ShouldBe(new StructuredDataContentDelta("{}"));

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
    public void ReasoningContentDelta_Equality_WhenSameValues_InstancesAreEqual() =>
        new ReasoningContentDelta("thinking", ExtensionData.Empty).ShouldBe(
            new ReasoningContentDelta("thinking", ExtensionData.Empty));

    [Fact]
    public void ToolArgumentsContentDelta_Constructor_WhenFragmentNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolArgumentsContentDelta(new ToolCallId(Guid.NewGuid()), null!));

        exception.ParamName.ShouldBe("jsonFragment");
    }

    [Fact]
    public void ToolArgumentsContentDelta_Constructor_WhenValid_RoundTripsProperties()
    {
        var callId = new ToolCallId(Guid.NewGuid());

        var delta = new ToolArgumentsContentDelta(callId, "{}");

        delta.ToolCallId.ShouldBe(callId);
        delta.JsonFragment.ShouldBe("{}");
    }

    [Fact]
    public void ToolArgumentsContentDelta_Equality_WhenSameValues_InstancesAreEqual()
    {
        var callId = new ToolCallId(Guid.NewGuid());

        new ToolArgumentsContentDelta(callId, "{}").ShouldBe(new ToolArgumentsContentDelta(callId, "{}"));
    }

    [Fact]
    public void ProviderContentDelta_Constructor_WhenExtensionsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ProviderContentDelta(new ProviderId("openai"), null!));

        exception.ParamName.ShouldBe("extensions");
    }

    [Fact]
    public void ProviderContentDelta_Constructor_WhenValid_RoundTripsProperties()
    {
        var providerId = new ProviderId("openai");

        var delta = new ProviderContentDelta(providerId, ExtensionData.Empty);

        delta.ProviderId.ShouldBe(providerId);
        delta.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void ProviderContentDelta_Equality_WhenSameValues_InstancesAreEqual()
    {
        var providerId = new ProviderId("openai");

        new ProviderContentDelta(providerId, ExtensionData.Empty).ShouldBe(
            new ProviderContentDelta(providerId, ExtensionData.Empty));
    }

    [Fact]
    public void ContentDelta_Hierarchy_EveryLeafDerivesFromContentDelta()
    {
        ContentDelta text = new TextContentDelta("x");
        ContentDelta structuredData = new StructuredDataContentDelta("{}");
        ContentDelta reasoning = new ReasoningContentDelta("x", ExtensionData.Empty);
        ContentDelta toolArguments = new ToolArgumentsContentDelta(new ToolCallId(Guid.NewGuid()), "{}");
        ContentDelta provider = new ProviderContentDelta(new ProviderId("p"), ExtensionData.Empty);

        _ = text.ShouldBeOfType<TextContentDelta>();
        _ = structuredData.ShouldBeOfType<StructuredDataContentDelta>();
        _ = reasoning.ShouldBeOfType<ReasoningContentDelta>();
        _ = toolArguments.ShouldBeOfType<ToolArgumentsContentDelta>();
        _ = provider.ShouldBeOfType<ProviderContentDelta>();
    }
}
