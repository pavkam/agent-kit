// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit;

/// <summary>Verifies AgentCapabilityReference behavior and contracts.</summary>
public sealed class AgentCapabilityReferenceTests
{
    [Fact]
    public void AgentCapabilityReference_Constructor_RoundTripsProperties()
    {
        var capabilityId = new CapabilityId("cap");
        var profileId = new CapabilityProfileId("profile");
        var reference = new AgentCapabilityReference(capabilityId, profileId);
        reference.CapabilityId.ShouldBe(capabilityId);
        reference.ProfileId.ShouldBe(profileId);
    }

    [Fact]
    public void AgentCapabilityReference_Equality_WhenSameValues_InstancesAreEqual()
    {
        var capabilityId = new CapabilityId("cap");
        var profileId = new CapabilityProfileId("profile");
        new AgentCapabilityReference(capabilityId, profileId).ShouldBe(new AgentCapabilityReference(capabilityId, profileId));
    }

    [Fact]
    public void AgentCapabilityReference_Equality_WhenCapabilityDiffers_InstancesAreNotEqual()
    {
        var profileId = new CapabilityProfileId("profile");
        new AgentCapabilityReference(new CapabilityId("first"), profileId).ShouldNotBe(new AgentCapabilityReference(new CapabilityId("second"), profileId));
    }

    [Fact]
    public void AgentCapabilityReference_Equality_WhenProfileDiffers_InstancesAreNotEqual()
    {
        var capabilityId = new CapabilityId("cap");
        new AgentCapabilityReference(capabilityId, new CapabilityProfileId("first")).ShouldNotBe(new AgentCapabilityReference(capabilityId, new CapabilityProfileId("second")));
    }

    [Fact]
    public void AgentCapabilityReference_Constructor_WhenCapabilityIdIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new AgentCapabilityReference(default, new CapabilityProfileId("profile")));
        exception.ParamName.ShouldBe("capabilityId");
    }

    [Fact]
    public void AgentCapabilityReference_Constructor_WhenProfileIdIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new AgentCapabilityReference(new CapabilityId("cap"), default));
        exception.ParamName.ShouldBe("profileId");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentCapabilityReference(new CapabilityId("cap"), new CapabilityProfileId("profile"));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
