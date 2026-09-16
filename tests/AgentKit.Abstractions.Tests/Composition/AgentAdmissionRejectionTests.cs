// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentAdmissionRejection behavior and contracts.</summary>
public sealed class AgentAdmissionRejectionTests
{
    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new AgentAdmissionRejection(default, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), "reason")).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenReasonIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new AgentAdmissionRejection(CompositionTestData.AgentId, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), " ")).ParamName.ShouldBe("reason");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var revision = new AgentDefinitionRevision(1);
        var version = new AgentCatalogVersion(2);
        var rejection = new AgentAdmissionRejection(CompositionTestData.AgentId, revision, version, "unavailable");
        rejection.AgentId.ShouldBe(CompositionTestData.AgentId);
        rejection.PinnedRevision.ShouldBe(revision);
        rejection.CatalogVersion.ShouldBe(version);
        rejection.Reason.ShouldBe("unavailable");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AgentAdmissionRejection(CompositionTestData.AgentId, new AgentDefinitionRevision(1), new AgentCatalogVersion(2), "unavailable");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
