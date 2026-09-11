// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddLanguageTool_WhenCalledTwice_RegistersOneToolDescriptor()
    {
        var services = new ServiceCollection();
        _ = services.AddLanguageTool();
        _ = services.AddLanguageTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(LanguageTool)).ShouldBe(1);
    }
}
