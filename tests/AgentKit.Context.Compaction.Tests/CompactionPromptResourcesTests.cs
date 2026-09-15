// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>Verifies the embedded default prompt is present, loadable, and cached.</summary>
public sealed class CompactionPromptResourcesTests
{
    [Fact]
    public void DefaultSummaryPrompt_WhenAccessed_IsLoadedFromEmbeddedResourceAndNonEmpty()
    {
        var resourceNames = typeof(CompactionPromptResources).Assembly.GetManifestResourceNames();

        var prompt = CompactionPromptResources.DefaultSummaryPrompt;

        resourceNames.ShouldContain(CompactionPromptResources.DefaultSummaryPromptResourceName);
        prompt.ShouldNotBeNullOrWhiteSpace();
        prompt.ShouldNotStartWith("\uFEFF");
        prompt.ShouldBe(prompt.TrimEnd());
    }

    [Fact]
    public void DefaultSummaryPrompt_WhenAccessedTwice_ReturnsTheSameCachedInstance()
    {
        var first = CompactionPromptResources.DefaultSummaryPrompt;

        var second = CompactionPromptResources.DefaultSummaryPrompt;

        ReferenceEquals(first, second).ShouldBeTrue();
    }

    [Fact]
    public void DefaultSummaryPrompt_WhenAccessed_MatchesTheEmbeddedResourceText()
    {
        using var stream = typeof(CompactionPromptResources).Assembly
            .GetManifestResourceStream(CompactionPromptResources.DefaultSummaryPromptResourceName)
            .ShouldNotBeNull();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var expected = reader.ReadToEnd().TrimEnd();

        CompactionPromptResources.DefaultSummaryPrompt.ShouldBe(expected);
    }

    [Theory]
    [InlineData("do not invent")]
    [InlineData("plain text")]
    [InlineData("instruction")]
    public void DefaultSummaryPrompt_WhenAccessed_StatesTheCoreSummaryConstraints(string requiredPhrase) =>
        CompactionPromptResources.DefaultSummaryPrompt.ShouldContain(requiredPhrase, Case.Insensitive);
}
