// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextManifest"/> invariants.</summary>
public sealed class ContextManifestTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_PreservesEvidence()
    {
        var manifest = new ContextManifest(
            new ModelRequestId(Guid.Parse("80000000-0000-0000-0000-000000000001")),
            new AgentDefinitionRevision(1),
            new SessionVersion(1),
            new ConfigurationVersion(1),
            new ContextContributorCatalogVersion(1),
            ContextTestData.Model(),
            [],
            new ContextCostEstimate(0, 0));
        manifest.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenModelRequestIdIsDefault_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ContextManifest(
            default,
            new AgentDefinitionRevision(1),
            new SessionVersion(1),
            new ConfigurationVersion(1),
            new ContextContributorCatalogVersion(1),
            ContextTestData.Model(),
            [],
            new ContextCostEstimate(0, 0))).ParamName.ShouldBe("modelRequestId");
}
