// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolCatalogSelectionTests
{

    [Fact]
    public void Constructor_WhenExactChoicesProvided_NormalizesAliasesAndPreservesValueSemantics()
    {
        var first = ToolCatalogMergeTestData.Candidate();
        var second = ToolCatalogMergeTestData.Candidate("other", "other", "other", "Other");
        var aliases = ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), first).Add(new ToolAlias("Other"), second);
        var selection = new ToolCatalogSelection([first, second], aliases);
        selection.Tools.ShouldBe([first, second]);
        selection.Aliases.Count.ShouldBe(2);
        var copy = new ToolCatalogSelection([first, second], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("Other"), second).Add(new ToolAlias("read"), first));
        selection.ShouldBe(copy);
        selection.GetHashCode().ShouldBe(copy.GetHashCode());
        selection.ShouldNotBe(new ToolCatalogSelection([second, first], aliases));
        selection.ShouldNotBe(new ToolCatalogSelection([first, second], []));
        selection.Equals(null).ShouldBeFalse();
        new ToolCatalogSelection([], []).Tools.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenEvidenceInvalid_RejectsExactParameter()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        Exact<ArgumentException>(() => _ = new ToolCatalogSelection(default, []), "tools");
        Exact<ArgumentException>(() => _ = new ToolCatalogSelection([null!], []), "tools");
        Exact<ArgumentException>(() => _ = new ToolCatalogSelection([candidate, candidate], []), "tools");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogSelection([], null!), "aliases");
        Exact<ArgumentOutOfRangeException>(() => _ = new ToolCatalogSelection([], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(default, candidate)), "aliases");
        Exact<ArgumentNullException>(() => _ = new ToolCatalogSelection([], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("read"), null!)), "aliases");
        Exact<ArgumentException>(() => _ = new ToolCatalogSelection([], ImmutableDictionary<ToolAlias, ToolCatalogCandidate>.Empty.Add(new ToolAlias("fabricated"), candidate)), "aliases");
    }

    [Fact]
    public void Constructor_WhenComparerWeakensAliasIdentity_RejectsNormalizedCollisionsAndWrongCase()
    {
        var candidate = ToolCatalogMergeTestData.Candidate();
        var unequal = EqualityComparer<ToolAlias>.Create(static (_, _) => false, static value => value.GetHashCode());
        var duplicates = ImmutableDictionary.Create<ToolAlias, ToolCatalogCandidate>(unequal).Add(new ToolAlias("read"), candidate).Add(new ToolAlias("read"), candidate);
        Exact<ArgumentException>(() => _ = new ToolCatalogSelection([candidate], duplicates), "aliases");
        var insensitive = EqualityComparer<ToolAlias>.Create(static (a, b) => StringComparer.OrdinalIgnoreCase.Equals(a.Value, b.Value), static value => StringComparer.OrdinalIgnoreCase.GetHashCode(value.Value));
        var wrongCase = ImmutableDictionary.Create<ToolAlias, ToolCatalogCandidate>(insensitive).Add(new ToolAlias("READ"), candidate);
        Exact<ArgumentException>(() => _ = new ToolCatalogSelection([candidate], wrongCase), "aliases");
    }

    private static void Exact<TException>(Action action, string parameter) where TException : ArgumentException
    {
        var exception = Should.Throw<TException>(action);
        exception.GetType().ShouldBe(typeof(TException));
        exception.ParamName.ShouldBe(parameter);
    }
}
