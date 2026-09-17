// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

public sealed class CodingAgentThemeTests
{
    [Fact]
    public void Slugs_WhenRead_ContainTheDefault() =>
        CodingAgentTheme.Slugs.ShouldContain(CodingAgentTheme.DefaultSlug);

    [Theory]
    [InlineData(null, CodingAgentTheme.DefaultSlug)]
    [InlineData("", CodingAgentTheme.DefaultSlug)]
    [InlineData("  ", CodingAgentTheme.DefaultSlug)]
    [InlineData("not-a-theme", CodingAgentTheme.DefaultSlug)]
    [InlineData("nord", "nord")]
    [InlineData("  Dracula  ", "dracula")]
    public void ResolveSlug_WhenOverrideVaries_FallsBackOnlyForUnknownValues(string? configured, string expected) =>
        CodingAgentTheme.ResolveSlug(configured).ShouldBe(expected);

    [Fact]
    public void Load_WhenSlugIsBundled_ReturnsAFrozenTheme() =>
        CodingAgentTheme.Load("nord").IsFrozen.ShouldBeTrue();

    [Fact]
    public void Load_WhenSlugIsBlank_Throws() =>
        Should.Throw<ArgumentException>(() => CodingAgentTheme.Load(" ")).ParamName.ShouldBe("slug");

    [Theory]
    [InlineData("turbo-vision", "Turbo Vision")]
    [InlineData("tokyo-night-storm", "Tokyo Night Storm")]
    [InlineData("nord", "Nord")]
    public void DisplayName_WhenSlugIsHyphenated_CapitalizesEachWord(string slug, string expected) =>
        CodingAgentTheme.DisplayName(slug).ShouldBe(expected);

    [Fact]
    public void DisplayName_WhenSlugIsBlank_Throws() =>
        Should.Throw<ArgumentException>(() => CodingAgentTheme.DisplayName("")).ParamName.ShouldBe("slug");
}
