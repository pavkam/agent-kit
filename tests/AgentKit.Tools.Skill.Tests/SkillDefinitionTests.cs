// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill.Tests;

public sealed class SkillDefinitionTests
{
    [Fact]
    public void Equals_WhenValuesMatch_ReturnsTrueAndSameHashCode()
    {
        var path = new FileSystemPath("private/source.md");
        var first = new SkillDefinition(new SkillId("docs"), "Documentation", "Project documentation.", SkillTrust.Workspace, path);
        var second = new SkillDefinition(new SkillId("docs"), "Documentation", "Project documentation.", SkillTrust.Workspace, path);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldContain("Documentation");
    }

    [Fact]
    public void With_WhenNoMembersChange_ReturnsEqualCopyThroughGeneratedCloneConstructor()
    {
        var original = new SkillDefinition(
            new SkillId("docs"), "Documentation", "Project documentation.", SkillTrust.Workspace, new FileSystemPath("private/source.md"));

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_WhenNameIsBlank_ThrowsArgumentException(string? name)
    {
        _ = Should.Throw<ArgumentException>(() => new SkillDefinition(
            new SkillId("docs"), name!, "description", SkillTrust.Workspace, new FileSystemPath("a.md")));
    }
}
