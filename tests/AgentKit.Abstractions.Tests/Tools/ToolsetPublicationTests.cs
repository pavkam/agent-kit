// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

public sealed class ToolsetPublicationTests
{
    [Fact]
    public void Constructor_WhenKeyDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolsetPublication(
            default, new ToolsetVersion(1), Policy(), [], []));

        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void Constructor_WhenExecutionPolicyNull_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolsetPublication(
            new ToolsetKey("tools"), new ToolsetVersion(1), null!, [], []));

        exception.ParamName.ShouldBe("executionPolicy");
    }

    [Fact]
    public void Constructor_WhenVersionDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolsetPublication(
            new ToolsetKey("tools"), default, Policy(), [], []));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenSourcesDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolsetPublication(
            new ToolsetKey("tools"), new ToolsetVersion(1), Policy(), default, []));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Constructor_WhenAliasesDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolsetPublication(
            new ToolsetKey("tools"), new ToolsetVersion(1), Policy(), [], default));

        exception.ParamName.ShouldBe("aliases");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenArrayContainsNull_ThrowsExactException(bool sourceArray)
    {
        var exception = sourceArray
            ? Should.Throw<ArgumentException>(() => Create([null!], []))
            : Should.Throw<ArgumentException>(() => Create(
                [new ToolsetSourceSelection(new ToolSourceId("source"))], [null!]));

        exception.ParamName.ShouldBe(sourceArray ? "sources" : "aliases");
    }

    [Fact]
    public void Constructor_WhenEmptyPublication_RetainsInitializedEmptyMembership()
    {
        var publication = Create([], []);

        publication.Sources.ShouldBeEmpty();
        publication.Aliases.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenSourceDuplicates_ThrowsExactException()
    {
        var source = new ToolsetSourceSelection(new ToolSourceId("source"));

        var exception = Should.Throw<ArgumentException>(() => Create([source, source], []));

        exception.ParamName.ShouldBe("sources");
    }

    [Fact]
    public void Constructor_WhenAliasDuplicates_ThrowsExactException()
    {
        var source = new ToolsetSourceSelection(new ToolSourceId("source"));
        var first = new ToolAliasAssignment(new ToolAlias("tool"), Tool("one"));
        var second = new ToolAliasAssignment(new ToolAlias("tool"), Tool("two"));

        var exception = Should.Throw<ArgumentException>(() => Create([source], [first, second]));

        exception.ParamName.ShouldBe("aliases");
    }

    [Fact]
    public void Constructor_WhenAliasesHaveNoSource_ThrowsExactException()
    {
        var alias = new ToolAliasAssignment(new ToolAlias("tool"), Tool("one"));

        var exception = Should.Throw<ArgumentException>(() => Create([], [alias]));

        exception.ParamName.ShouldBe("aliases");
    }

    [Fact]
    public void Constructor_WhenMultipleAliasesTargetSameIdentity_PreservesBothAssignments()
    {
        var source = new ToolsetSourceSelection(new ToolSourceId("source"));
        var identity = Tool("one");

        var publication = Create(
            [source],
            [
                new ToolAliasAssignment(new ToolAlias("first"), identity),
                new ToolAliasAssignment(new ToolAlias("second"), identity),
            ]);

        publication.Aliases.Select(static assignment => assignment.Tool).ShouldAllBe(tool => tool == identity);
    }

    [Fact]
    public void Equality_WhenMembershipOrderDiffers_IsStructuralAndOrdered()
    {
        var firstSource = new ToolsetSourceSelection(new ToolSourceId("first"));
        var secondSource = new ToolsetSourceSelection(new ToolSourceId("second"));
        var alias = new ToolAliasAssignment(new ToolAlias("tool"), Tool("one"));
        var first = Create([firstSource, secondSource], [alias]);
        var same = Create([firstSource, secondSource], [alias]);
        var reordered = Create([secondSource, firstSource], [alias]);

        first.ShouldBe(same);
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.ShouldNotBe(reordered);
    }

    private static ToolsetPublication Create(
        ImmutableArray<ToolsetSourceSelection> sources,
        ImmutableArray<ToolAliasAssignment> aliases) => new(
        new ToolsetKey("tools"), new ToolsetVersion(1), Policy(), sources, aliases);

    private static ToolExecutionPolicyReference Policy() => new(
        new ToolExecutionPolicyKey("policy"), new ToolExecutionPolicyVersion(1));

    private static ToolIdentity Tool(string id) => new(new ToolId(id), new ToolVersion("1.0"));
}
