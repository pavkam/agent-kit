// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationRunManifestTests
{
    private static EvaluationRunManifest Build(
        AgentId? agent = null,
        long revision = 1,
        long catalog = 1,
        SessionProfileKey? profile = null,
        ImmutableArray<string>? candidates = null,
        ImmutableArray<EvaluationModelUse>? used = null) =>
        new(
            agent ?? EvaluationTestData.Agent,
            new AgentDefinitionRevision(revision),
            new AgentCatalogVersion(catalog),
            profile ?? EvaluationTestData.SessionProfile,
            candidates ?? ["chat"],
            used ?? []);

    [Fact]
    public void Constructor_WhenAgentIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(agent: default(AgentId))).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenSessionProfileIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Build(profile: default(SessionProfileKey))).ParamName.ShouldBe("sessionProfile");

    [Fact]
    public void Constructor_WhenArraysAreDefaultOrContainBlankItems_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new EvaluationRunManifest(EvaluationTestData.Agent, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), EvaluationTestData.SessionProfile, default, []))
            .ParamName.ShouldBe("modelCandidates");
        Should.Throw<ArgumentException>(() => Build(candidates: [" "])).ParamName.ShouldBe("modelCandidates");
        Should.Throw<ArgumentException>(() => new EvaluationRunManifest(EvaluationTestData.Agent, new AgentDefinitionRevision(1), new AgentCatalogVersion(1), EvaluationTestData.SessionProfile, [], default))
            .ParamName.ShouldBe("modelsUsed");
        Should.Throw<ArgumentException>(() => Build(used: [null!])).ParamName.ShouldBe("modelsUsed");
    }

    [Fact]
    public void Equals_WhenArraysMatchByValue_IsEqualAndHashesAlike()
    {
        var left = Build(used: [new EvaluationModelUse("p", "f", "m", null)]);
        var right = Build(used: [new EvaluationModelUse("p", "f", "m", null)]);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(Build(candidates: ["other"]));
    }
}
