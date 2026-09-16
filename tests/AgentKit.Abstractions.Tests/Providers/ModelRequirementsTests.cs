// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelRequirements behavior and contracts.</summary>
public sealed class ModelRequirementsTests
{
    [Fact]
    public void None_WhenRead_HasNoRequirements()
    {
        var none = ModelRequirements.None;
        none.RequiresSystemInstructions.ShouldBeFalse();
        none.RequiresStreaming.ShouldBeFalse();
        none.RequiresToolCalls.ShouldBeFalse();
        none.RequiresParallelToolCalls.ShouldBeFalse();
        none.RequiresStructuredOutput.ShouldBeFalse();
        none.RequiresReasoning.ShouldBeFalse();
        none.RequiresVisionInput.ShouldBeFalse();
        none.MinimumInputTokens.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MinimumInputTokens_WhenNotPositive_ThrowsExactArgumentOutOfRangeException(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ModelRequirements { MinimumInputTokens = value });
        exception.ParamName.ShouldBe("MinimumInputTokens");
    }

    [Fact]
    public void Constructor_WhenEveryFlagIsSet_RoundTripsProperties()
    {
        var requirements = new ModelRequirements
        {
            RequiresSystemInstructions = true,
            RequiresStreaming = true,
            RequiresToolCalls = true,
            RequiresParallelToolCalls = true,
            RequiresStructuredOutput = true,
            RequiresReasoning = true,
            RequiresVisionInput = true,
            MinimumInputTokens = 1024,
        };
        requirements.RequiresSystemInstructions.ShouldBeTrue();
        requirements.RequiresStreaming.ShouldBeTrue();
        requirements.RequiresToolCalls.ShouldBeTrue();
        requirements.RequiresParallelToolCalls.ShouldBeTrue();
        requirements.RequiresStructuredOutput.ShouldBeTrue();
        requirements.RequiresReasoning.ShouldBeTrue();
        requirements.RequiresVisionInput.ShouldBeTrue();
        requirements.MinimumInputTokens.ShouldBe(1024);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ModelRequirements { RequiresStreaming = true };
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
