// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit.TestSupport;

public sealed class ToolSchemaProfileTests
{
    [Fact]
    public void Constructor_WhenIdentityInvalid_RejectsExactArgument()
    {
        Should.Throw<ArgumentNullException>(() => new ToolSchemaProfile(default, new(1), ToolSchemaTestData.Dialect, [], [])).ParamName.ShouldBe("id");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolSchemaProfile(new("profile"), default, ToolSchemaTestData.Dialect, [], [])).ParamName.ShouldBe("version");
        Should.Throw<ArgumentNullException>(() => new ToolSchemaProfile(new("profile"), new(1), default, [], [])).ParamName.ShouldBe("dialect");
    }

    [Fact]
    public void Constructor_WhenKeywordSetsInvalid_RejectsExactCollection()
    {
        ImmutableArray<string>[] assertions = [default, [null!], [""], [" "], ["type", "type"]];
        foreach (var keywords in assertions)
        {
            Should.Throw<ArgumentException>(() => Create(keywords, [])).ParamName.ShouldBe("assertionKeywords");
        }
        ImmutableArray<string>[] annotations = [default, [null!], [""], [" "], ["title", "title"], ["type"]];
        foreach (var keywords in annotations)
        {
            Should.Throw<ArgumentException>(() => Create(["type"], keywords)).ParamName.ShouldBe("annotationKeywords");
        }
    }

    [Fact]
    public void Equals_WhenKeywordOrderDiffers_UsesNormalizedCompleteEvidence()
    {
        var first = Create(["type", "properties"], ["title", "description"]);
        var same = Create(["properties", "type"], ["description", "title"]);
        first.ShouldBe(same); first.GetHashCode().ShouldBe(same.GetHashCode());
        first.AssertionKeywords.ShouldBe(["properties", "type"]); first.AnnotationKeywords.ShouldBe(["description", "title"]);
        first.ShouldNotBe(Create(["type"], ["description", "title"]));
        first.ShouldNotBe(new ToolSchemaProfile(new("other"), new(1), ToolSchemaTestData.Dialect, first.AssertionKeywords, first.AnnotationKeywords));
        first.ShouldNotBe(new ToolSchemaProfile(new("profile"), new(2), ToolSchemaTestData.Dialect, first.AssertionKeywords, first.AnnotationKeywords));
        first.ShouldNotBe(new ToolSchemaProfile(new("profile"), new(1), new("urn:other"), first.AssertionKeywords, first.AnnotationKeywords));
        first.Equals(null).ShouldBeFalse();
        Create([], []).AssertionKeywords.ShouldBeEmpty();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Create(["type"], ["title"]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static ToolSchemaProfile Create(ImmutableArray<string> assertions, ImmutableArray<string> annotations) =>
        new(new("profile"), new(1), ToolSchemaTestData.Dialect, assertions, annotations);
}
