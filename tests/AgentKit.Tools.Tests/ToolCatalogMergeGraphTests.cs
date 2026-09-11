// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

public sealed class ToolCatalogMergeGraphTests
{
    [Fact]
    public void Apply_WhenDisplayNamesRepeatAndVersionsDiffer_UsesOnlyExplicitAliasesAndExactIdentities()
    {
        var first = ToolCaptureTestData.Descriptor(version: "1");
        var second = ToolCaptureTestData.Descriptor(version: "2");
        var source = ToolCaptureTestData.Snapshot([second, first]);
        var toolset = ToolCatalogMergeTestData.Toolset("tools", [source], [ToolCatalogMergeTestData.Alias("version-two", second), ToolCatalogMergeTestData.Alias("version-one", first), ToolCatalogMergeTestData.Alias("also-one", first)]);
        var graph = Graph([toolset], [source]);
        graph.Context.Collisions.ShouldBeEmpty();
        var aliases = ImmutableDictionary.CreateBuilder<ToolAlias, ToolCatalogCandidate>();
        foreach (var candidate in graph.Context.Candidates)
        {
            foreach (var assignment in toolset.Aliases.Where(assignment => assignment.Tool == candidate.Identity)) { aliases.Add(assignment.Alias, candidate); }
        }
        var snapshot = graph.Apply(new ToolCatalogSelection(graph.Context.Candidates, aliases.ToImmutable()), new ToolCatalogVersion("catalog"));
        snapshot.Tools.ShouldBe([second, first]);
        snapshot.ProviderAliases.Count.ShouldBe(3);
        snapshot.ProviderAliases.ContainsKey(new ToolAlias(first.Name)).ShouldBeFalse();
        snapshot.ProviderAliases.ContainsKey(new ToolAlias(first.Id.Value)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("descriptor")]
    [InlineData("source-version")]
    [InlineData("tool-version")]
    [InlineData("alias")]
    [InlineData("toolset-version")]
    public void Apply_WhenPolicyReconstructsChangedEvidence_RejectsEverySubstitution(string change)
    {
        var original = ToolCatalogMergeTestData.Candidate();
        var tool = ToolCaptureTestData.Descriptor(version: change == "tool-version" ? "2" : "1", description: change == "descriptor" ? "substituted-description" : original.Tool.Description);
        var source = ToolCatalogMergeTestData.Source(original.Source.SourceId.Value, [tool], change == "source-version" ? "new-source" : original.Source.SourceVersion.Value);
        var toolset = ToolCatalogMergeTestData.Toolset("tools", [source], [ToolCatalogMergeTestData.Alias(change == "alias" ? "invented" : "read", tool)], version: change == "toolset-version" ? 2 : 1);
        var changed = new ToolCatalogCandidate(toolset, source, new(tool.Id, tool.Version));
        var graph = Graph([original.Toolset], [original.Source]);
        var selection = new ToolCatalogSelection([changed], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(toolset.Aliases[0].Alias, changed));
        _ = Should.Throw<InvalidOperationException>(() => graph.Apply(selection, new ToolCatalogVersion("catalog")));
    }

    [Fact]
    public void Constructor_WhenMultipleCollisionKindsExist_ReportsCompleteDeterministicEvidence()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "source.first");
        var second = ToolCatalogMergeTestData.Candidate("second", "source.second");
        var missing = ToolCatalogMergeTestData.Alias("missing-alias", ToolCaptureTestData.Descriptor("missing-tool"));
        var third = ToolCatalogMergeTestData.Toolset("third", [first.Source], [missing], "other-policy");
        var graph = new ToolCatalogMergeGraph(ToolCatalogMergeTestData.Request([second.Toolset, first.Toolset, third]), [second.Toolset, first.Toolset, third],
            ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(first.Source.SourceId, first.Source).Add(second.Source.SourceId, second.Source));

        graph.Context.Candidates.Select(static candidate => candidate.Toolset.Key.Value).ShouldBe(["second", "first", "third"]);
        graph.Context.Collisions.Length.ShouldBe(3);
        graph.Context.Collisions[0].ShouldBeOfType<ToolCatalogIdentityCollision>().Candidates.ShouldBe(graph.Context.Candidates);
        graph.Context.Collisions[1].ShouldBeOfType<ToolCatalogAliasCollision>().Candidates.ShouldBe(graph.Context.Candidates[..2]);
        graph.Context.Collisions[2].ShouldBeOfType<ToolCatalogMissingAliasTarget>().Assignment.ShouldBe(missing);
        graph.Aliases.ShouldBe([new ToolAlias("read"), new ToolAlias("missing-alias")]);
    }

    [Fact]
    public void Constructor_WhenAliasTargetExistsOnlyInAnotherToolset_ReportsMissingMembership()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var empty = ToolCatalogMergeTestData.Source("empty", []);
        var foreign = ToolCatalogMergeTestData.Toolset("foreign", [empty], first.Toolset.Aliases);
        var graph = Graph([first.Toolset, foreign], [first.Source, empty]);

        graph.Context.Collisions.Length.ShouldBe(2);
        graph.Context.Collisions[0].ShouldBeOfType<ToolCatalogAliasCollision>().Alias.ShouldBe(new ToolAlias("read"));
        graph.Context.Collisions[0].ShouldBeOfType<ToolCatalogAliasCollision>().MissingTargets.Single().Toolset.ShouldBeSameAs(foreign);
        graph.Context.Collisions[1].ShouldBeOfType<ToolCatalogMissingAliasTarget>().Toolset.ShouldBeSameAs(foreign);
        _ = Should.Throw<InvalidOperationException>(() => graph.Apply(new ToolCatalogSelection([first], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), first)), new ToolCatalogVersion("1")));
    }

    [Fact]
    public void Apply_WhenSelectionOrderDiffers_PreservesAuthoredIdentityOrderAndEmptySources()
    {
        var first = ToolCatalogMergeTestData.Candidate("first", "source.first", "tool.first", "first");
        var second = ToolCatalogMergeTestData.Candidate("second", "source.second", "tool.second", "second");
        var empty = ToolCatalogMergeTestData.Source("empty", []);
        var emptySet = ToolCatalogMergeTestData.Toolset("empty", [empty], []);
        var graph = Graph([first.Toolset, second.Toolset, emptySet], [second.Source, empty, first.Source]);
        var snapshot = graph.Apply(new ToolCatalogSelection([second, first], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("second"), second).Add(new ToolAlias("first"), first)), new ToolCatalogVersion("catalog"));

        snapshot.Tools.ShouldBe([first.Tool, second.Tool]);
        snapshot.SourceVersions[empty.SourceId].ShouldBe(empty.SourceVersion);
        snapshot.ExecutionPolicies[first.Identity].ShouldBe(first.Toolset.ExecutionPolicy);
        snapshot.ProviderAliases[new ToolAlias("first")].ShouldBe(first.Identity);
        snapshot.RunId.ShouldBe(graph.Context.Request.RunId);
        snapshot.Identity.ShouldBe(graph.Context.Request.Identity);
        snapshot.SecurityPolicy.ShouldBe(graph.Context.Request.Authorization.PolicySnapshot);
        snapshot.AgentDefinitionRevision.ShouldBe(graph.Context.Request.AgentDefinitionRevision);
        snapshot.ConfigurationVersion.ShouldBe(graph.Context.Request.Configuration.Version);
    }

    [Fact]
    public void Constructor_WhenEveryRepeatedAliasTargetIsMissing_PreservesAliasCollisionAndEveryMissingAssignment()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var empty = ToolCatalogMergeTestData.Source("empty", []);
        var first = ToolCatalogMergeTestData.Toolset("first", [empty], candidate.Toolset.Aliases);
        var second = ToolCatalogMergeTestData.Toolset("second", [empty], candidate.Toolset.Aliases);
        var graph = Graph([first, second], [empty]);
        graph.Context.Candidates.ShouldBeEmpty();
        graph.Context.Collisions.Length.ShouldBe(3);
        var alias = graph.Context.Collisions[0].ShouldBeOfType<ToolCatalogAliasCollision>();
        alias.Candidates.ShouldBeEmpty();
        alias.MissingTargets.Select(static missing => missing.Toolset.Key).ShouldBe([first.Key, second.Key]);
        graph.Context.Collisions[1].ShouldBeOfType<ToolCatalogMissingAliasTarget>().Toolset.ShouldBeSameAs(first);
        graph.Context.Collisions[2].ShouldBeOfType<ToolCatalogMissingAliasTarget>().Toolset.ShouldBeSameAs(second);
    }

    [Fact]
    public void Apply_WhenAliasHasEquivalentOrigin_PreservesMultipleExplicitAliasesForOneSelectedBinding()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var secondSet = ToolCatalogMergeTestData.Toolset("second", [first.Source], [ToolCatalogMergeTestData.Alias("also-read", first.Tool)]);
        var second = new ToolCatalogCandidate(secondSet, first.Source, first.Identity);
        var graph = Graph([first.Toolset, secondSet], [first.Source]);
        var snapshot = graph.Apply(new ToolCatalogSelection([first], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), first).Add(new ToolAlias("also-read"), second)), new ToolCatalogVersion("catalog"));

        snapshot.Tools.ShouldBe([first.Tool]);
        snapshot.ProviderAliases.Count.ShouldBe(2);
        snapshot.ProviderAliases.Values.ShouldAllBe(identity => identity == first.Identity);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("policy")]
    public void Apply_WhenAliasDisagreesWithSelectedBinding_RejectsEvenWhenBothCandidatesExist(string mismatch)
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = mismatch == "source" ? ToolCatalogMergeTestData.Candidate("second", "other") : new ToolCatalogCandidate(
            ToolCatalogMergeTestData.Toolset("second", [first.Source], first.Toolset.Aliases, "other"), first.Source, first.Identity);
        var graph = Graph([first.Toolset, second.Toolset], mismatch == "source" ? [first.Source, second.Source] : [first.Source]);
        var selection = new ToolCatalogSelection([first], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), second));
        _ = Should.Throw<InvalidOperationException>(() => graph.Apply(selection, new ToolCatalogVersion("catalog")));
    }

    [Fact]
    public void Apply_WhenPolicyOmitsOrInventsEvidence_RejectsInsteadOfPublishingPartialCatalog()
    {
        var original = ToolCatalogMergeTestData.Candidate();
        var foreign = ToolCatalogMergeTestData.Candidate(policy: "invented-policy");
        var graph = Graph([original.Toolset], [original.Source]);
        ToolCatalogSelection[] invalid = [new([], []), new([original], []), new([foreign], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), foreign))];
        foreach (var selection in invalid) { _ = Should.Throw<InvalidOperationException>(() => graph.Apply(selection, new ToolCatalogVersion("catalog"))); }
        Exact<ArgumentNullException>(() => graph.Apply(null!, new ToolCatalogVersion("catalog")), "selection");
        Exact<ArgumentOutOfRangeException>(() => graph.Apply(new([], []), default), "version");
    }

    [Fact]
    public void Constructor_WhenInputsMalformed_RejectsExactParameter()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var request = ToolCatalogMergeTestData.Request([candidate.Toolset]);
        var sources = ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(candidate.Source.SourceId, candidate.Source);
        Exact<ArgumentNullException>(() => _ = new ToolCatalogMergeGraph(null!, [], []), "request");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(request, default, sources), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(request, [null!], sources), "toolsets");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogMergeGraph(request, [candidate.Toolset], null!), "sources");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(request, [], sources), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(request, [ToolCatalogMergeTestData.Toolset("wrong", [candidate.Source], [])], sources), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(request, [ToolCatalogMergeTestData.Toolset("tools", [candidate.Source], [], "wrong")], sources), "toolsets");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(request, [candidate.Toolset], []), "sources");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(ToolCaptureTestData.Discovery(), [], sources), "sources");
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolCatalogMergeGraph(request, [candidate.Toolset], ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(default, candidate.Source)), "sources");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogMergeGraph(request, [candidate.Toolset], sources.SetItem(candidate.Source.SourceId, null!)), "sources");
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(request, [candidate.Toolset], ImmutableDictionary<ToolSourceId, ToolProviderSnapshot>.Empty.Add(new ToolSourceId("wrong"), candidate.Source)), "sources");
    }

    [Fact]
    public void Constructor_WhenComparerWeakensSourceIdentity_UsesExactDomainKeys()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var unequal = EqualityComparer<ToolSourceId>.Create(static (_, _) => false, static value => value.GetHashCode());
        var duplicates = ImmutableDictionary.Create<ToolSourceId, ToolProviderSnapshot>(unequal).Add(candidate.Source.SourceId, candidate.Source).Add(candidate.Source.SourceId, candidate.Source);
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(ToolCatalogMergeTestData.Request([candidate.Toolset]), [candidate.Toolset], duplicates), "sources");
        var insensitive = EqualityComparer<ToolSourceId>.Create(static (a, b) => StringComparer.OrdinalIgnoreCase.Equals(a.Value, b.Value), static value => StringComparer.OrdinalIgnoreCase.GetHashCode(value.Value));
        var wrongCase = ImmutableDictionary.Create<ToolSourceId, ToolProviderSnapshot>(insensitive).Add(new ToolSourceId("SOURCE.TESTS"), candidate.Source);
        Exact<ArgumentException>(() => _ = new ToolCatalogMergeGraph(ToolCatalogMergeTestData.Request([candidate.Toolset]), [candidate.Toolset], wrongCase), "sources");
    }

    private static ToolCatalogMergeGraph Graph(ImmutableArray<ToolsetPublication> toolsets, ImmutableArray<ToolProviderSnapshot> sources) =>
        new(ToolCatalogMergeTestData.Request(toolsets), toolsets, sources.ToImmutableDictionary(static source => source.SourceId));

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
