// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddPlanTool_WhenCalledTwice_RegistersEachDefaultOnce()
    {
        var services = new ServiceCollection();
        _ = services.AddPlanTool().AddPlanTool();
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(PlanTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(ITool) && descriptor.ImplementationType == typeof(TodoTool)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IPlanStateStore)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<PlanId>)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<SessionEntryId>)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IIdentifierGenerator<SecurityEnforcementIntentId>)).ShouldBe(1);
    }
}
