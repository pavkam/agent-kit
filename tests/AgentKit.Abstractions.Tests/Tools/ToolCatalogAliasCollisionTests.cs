// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolCatalogAliasCollisionTests
{
    [Fact]
    public void Constructor_WhenUnresolvedAssignmentsCompete_RetainsCompleteEvidenceAndValidatesEveryConstraint()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("second");
        var missing = new ToolCatalogMissingAliasTarget(second.Toolset, second.Toolset.Aliases[0]);
        var alias = new ToolAlias("read");
        var collision = new ToolCatalogAliasCollision(alias, [first], [missing]);
        collision.Candidates.ShouldBe([first]);
        collision.MissingTargets.ShouldBe([missing]);
        collision.ShouldBe(new ToolCatalogAliasCollision(alias, [first], [missing]));
        collision.GetHashCode().ShouldBe(new ToolCatalogAliasCollision(alias, [first], [missing]).GetHashCode());
        var firstMissing = new ToolCatalogMissingAliasTarget(first.Toolset, first.Toolset.Aliases[0]);
        new ToolCatalogAliasCollision(alias, [], [firstMissing, missing]).MissingTargets.Length.ShouldBe(2);
        collision.ShouldNotBe(new ToolCatalogAliasCollision(alias, [first], [firstMissing]));
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first], default), "missingTargets");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first], [null!]), "missingTargets");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first], [missing, missing]), "missingTargets");
        var other = ToolCatalogMergeTestData.Candidate("other", alias: "other");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first], [new ToolCatalogMissingAliasTarget(other.Toolset, other.Toolset.Aliases[0])]), "missingTargets");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [], [missing]), "candidates");
    }


    [Fact]
    public void Constructor_WhenAliasAuthoredByMultipleSources_PreservesOrderedEvidence()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other", "different");
        var collision = new ToolCatalogAliasCollision(new ToolAlias("read"), [first, second]);
        collision.Alias.ShouldBe(new ToolAlias("read"));
        collision.Candidates.ShouldBe([first, second]);
        collision.ShouldBe(new ToolCatalogAliasCollision(new ToolAlias("read"), [first, second]));
        collision.GetHashCode().ShouldBe(new ToolCatalogAliasCollision(new ToolAlias("read"), [first, second]).GetHashCode());
        collision.ShouldNotBe(new ToolCatalogAliasCollision(new ToolAlias("read"), [second, first]));
        collision.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenGroupInvalid_RejectsExactParameter()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other", "different", "other");
        var alias = new ToolAlias("read");
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolCatalogAliasCollision(default, [first, second]), "alias");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, default), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first, null!]), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, []), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first]), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first, first]), "candidates");
        Exact<ArgumentException>(() => _ = new ToolCatalogAliasCollision(alias, [first, second]), "candidates");
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
