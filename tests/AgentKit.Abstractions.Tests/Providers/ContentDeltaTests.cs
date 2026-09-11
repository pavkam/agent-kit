// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ContentDelta behavior and contracts.</summary>
public sealed class ContentDeltaTests
{
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
