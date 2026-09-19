// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookProfileSelectionRequestTests
{
    [Fact]
    public void Constructor_WhenNeitherSupplied_ExposesNullProperties()
    {
        var request = new HookProfileSelectionRequest(null, null);

        request.RequestedProfile.ShouldBeNull();
        request.AgentId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenBothSupplied_RoundTripsEveryProperty()
    {
        var profile = HookKernelTestData.Profile;
        var agentId = new AgentId(Guid.NewGuid());

        var request = new HookProfileSelectionRequest(profile, agentId);

        request.RequestedProfile.ShouldBe(profile);
        request.AgentId.ShouldBe(agentId);
    }
}
