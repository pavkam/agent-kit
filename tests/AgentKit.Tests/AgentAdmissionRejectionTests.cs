// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;
/// <summary>Verifies AgentAdmissionRejection behavior and contracts.</summary>
[Collection(AdmissionObservabilityGroup.Name)]
public sealed class AgentAdmissionRejectionTests
{
    [Fact]
    public void AgentAdmissionRejection_WhenArgumentsAreInvalid_ThrowsWithArgumentNames()
    {
        var defaultIdentity = Should.Throw<ArgumentOutOfRangeException>(() => new AgentAdmissionRejection(default, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), "removed"));
        var nullReason = Should.Throw<ArgumentNullException>(() => new AgentAdmissionRejection(CompositionTestData.AgentId, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), null!));
        var blankReason = Should.Throw<ArgumentException>(() => new AgentAdmissionRejection(CompositionTestData.AgentId, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), " "));
        defaultIdentity.ParamName.ShouldBe("agentId");
        nullReason.ParamName.ShouldBe("reason");
        blankReason.ParamName.ShouldBe("reason");
    }
}
