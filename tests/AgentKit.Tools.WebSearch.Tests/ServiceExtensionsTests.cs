// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddWebSearchTool_WhenCalledTwice_RegistersToolAndGeneratorOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddWebSearchTool().AddWebSearchTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(WebSearchTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<WebSearchRequestId>)).ShouldBe(1);
        services.Any(descriptor => descriptor.ServiceType == typeof(IWebSearchProvider)).ShouldBeFalse();
    }
}
