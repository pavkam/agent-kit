// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Task.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddTaskTool_WhenCalledTwice_RegistersOneToolAndOneIdentitySource()
    {
        var services = new ServiceCollection();
        _ = services.AddTaskTool().AddTaskTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(TaskTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<DelegationId>)).ShouldBe(1);
    }
}
