// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies LlmRequestSettings behavior and contracts.</summary>
public sealed class LlmRequestSettingsTests
{
    [Fact]
    public void ReasoningEffort_WhenConfigured_ParticipatesInStructuralEquality()
    {
        var low = LlmRequestSettings.Default with { ReasoningEffort = LlmReasoningEffort.Low };
        var high = LlmRequestSettings.Default with { ReasoningEffort = LlmReasoningEffort.High };

        low.ShouldNotBe(high);
        low.ReasoningEffort.ShouldBe(LlmReasoningEffort.Low);
    }

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
    public void Constructor_WhenMaxOutputTokensIsNegative_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LlmRequestSettings(null, null, -1, [], null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("maxOutputTokens");
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

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenTemperatureIsNotFinite_ThrowsExactArgumentOutOfRangeException(double temperature)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new LlmRequestSettings(temperature, null, null, [], null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("temperature");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WhenTopPIsNotFinite_ThrowsExactArgumentOutOfRangeException(double topP)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new LlmRequestSettings(null, topP, null, [], null, null, ExtensionData.Empty));
        exception.ParamName.ShouldBe("topP");
    }

    [Fact]
    public void WithExpression_WhenTemperatureIsNaN_ThrowsArgumentOutOfRangeException()
    {
        var settings = LlmRequestSettings.Default;
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = settings with { Temperature = double.NaN });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void WithExpression_WhenTopPIsInfinite_ThrowsArgumentOutOfRangeException()
    {
        var settings = LlmRequestSettings.Default;
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => _ = settings with { TopP = double.PositiveInfinity });
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void LlmRequestSettings_Equality_WhenTemperatureIsSameFiniteValue_IsReflexive()
    {
        // Equals previously used == directly on the double? fields, so an instance whose Temperature was NaN was
        // not equal to itself while GetHashCode remained stable - breaking reflexivity. Temperature/TopP now
        // reject non-finite values entirely, so this documents the intended reflexive, finite-only contract.
        var settings = LlmRequestSettings.Default with { Temperature = 0.7, TopP = 0.9 };

        settings.ShouldBe(settings);
        settings.Equals(settings).ShouldBeTrue();
    }
}
