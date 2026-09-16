// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Skill.Tests;

public sealed class SkillIdTests
{
    [Fact]
    public void ToString_WhenCalled_ReturnsTheStableValue()
    {
        var id = new SkillId("docs");

        id.ToString().ShouldBe("docs");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsBlank_ThrowsArgumentException(string? value) =>
        Should.Throw<ArgumentException>(() => new SkillId(value!));
}
