// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies LlmRequestSettings behavior and contracts.</summary>
public sealed class LlmRequestSettingsTests
{
    [Fact]
    public void LlmRequestSettings_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = LlmRequestSettings.Default with
        {
            Temperature = 0.5,
            StopSequences = ["a"]
        };
        var second = LlmRequestSettings.Default with
        {
            Temperature = 0.5,
            StopSequences = ["a"]
        };
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void LlmRequestSettings_Equality_WhenDifferentStopSequences_InstancesAreNotEqual()
    {
        var first = LlmRequestSettings.Default with
        {
            StopSequences = ["a"]
        };
        var second = LlmRequestSettings.Default with
        {
            StopSequences = ["b"]
        };
        first.ShouldNotBe(second);
    }

    [Fact]
    public void WithExpression_WhenMaxOutputTokensIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var settings = LlmRequestSettings.Default;
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = settings with { MaxOutputTokens = -1 });
        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(-1L);
    }

    [Fact]
    public void WithExpression_WhenStopSequencesIsDefault_ThrowsArgumentException()
    {
        var settings = LlmRequestSettings.Default;
        var exception = Should.Throw<ArgumentException>(() => _ = settings with { StopSequences = default });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenSettingsExtensionsIsNull_ThrowsArgumentNullException()
    {
        var settings = LlmRequestSettings.Default;
        var exception = Should.Throw<ArgumentNullException>(() => _ = settings with { Extensions = null! });
        exception.ParamName.ShouldBe("value");
    }
}
