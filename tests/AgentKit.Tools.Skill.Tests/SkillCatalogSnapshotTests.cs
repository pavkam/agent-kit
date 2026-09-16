// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill.Tests;

public sealed class SkillCatalogSnapshotTests
{
    [Fact]
    public void Equals_WhenValuesMatch_ReturnsTrueAndSameHashCode()
    {
        var skills = ImmutableArray.Create(new SkillDefinition(
            new SkillId("docs"), "Documentation", "Project documentation.", SkillTrust.Workspace, new FileSystemPath("a.md")));
        var first = new SkillCatalogSnapshot("v1", skills);
        var second = new SkillCatalogSnapshot("v1", skills);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldContain("v1");
    }

    [Fact]
    public void With_WhenNoMembersChange_ReturnsEqualCopyThroughGeneratedCloneConstructor()
    {
        var original = new SkillCatalogSnapshot("v1", []);

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_WhenVersionIsBlank_ThrowsArgumentException(string? version) =>
        Should.Throw<ArgumentException>(() => new SkillCatalogSnapshot(version!, []));
}
