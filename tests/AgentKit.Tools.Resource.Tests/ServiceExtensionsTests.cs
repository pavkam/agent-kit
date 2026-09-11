// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddResourceTool_WhenCalledTwice_AddsOneToolDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddResourceTool().AddResourceTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(ResourceTool)).ShouldBe(1);
    }
}
