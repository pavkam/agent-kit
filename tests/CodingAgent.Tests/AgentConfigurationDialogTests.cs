// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent.Tests;

using AgentKit;

using SharpVision.Layout;

public sealed class AgentConfigurationDialogTests
{
    [Fact]
    public void Constructor_WhenCreated_LoadsTheConfigurationIntoNativeControls()
    {
        // Arrange
        var configuration = CodingAgentConfiguration.CreateDefault() with
        {
            ModelId = "gpt-6-astra",
            ReasoningEffort = LlmReasoningEffort.High,
            MaximumTurns = 20,
        };

        // Act
        var dialog = new AgentConfigurationDialog(configuration, isBusy: false);

        // Assert
        dialog.Header.ShouldContain("Agent Configuration");
        dialog.Width.ShouldBe(Length.Percent(88));
        dialog.CanMove.ShouldBeFalse();
        dialog.CloseOnEscape.ShouldBeTrue();
        dialog.ModelSelector.Items.Count.ShouldBe(3);
        dialog.ModelSelector.SelectedIndex.ShouldBe(2);
        dialog.ModelSelector.AllowNull.ShouldBeFalse();
        dialog.ReasoningChoices.Length.ShouldBe(CodingAgentConfiguration.ReasoningEfforts.Length);
        dialog.ReasoningChoices.Select(static choice => choice.IsChecked).ShouldBe([false, false, false, true, false]);
        dialog.ReasoningChoices.ShouldAllBe(static choice => choice.GroupName == "reasoning-effort");
        dialog.SelectedReasoningEffort.ShouldBe(LlmReasoningEffort.High);
        dialog.TurnSlider.Minimum.ShouldBe(AgentConfigurationDialog.MinimumTurns);
        dialog.TurnSlider.Maximum.ShouldBe(AgentConfigurationDialog.MaximumTurns);
        dialog.TurnSlider.Value.ShouldBe(20);
        dialog.TurnSlider.Style.ShouldBeNull();
        dialog.TurnSlider.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        dialog.HasSelectedResult.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WhenTurnLimitIsOutsideTheSliderRange_ClampsIt()
    {
        var configuration = CodingAgentConfiguration.CreateDefault() with { MaximumTurns = 200 };

        var dialog = new AgentConfigurationDialog(configuration, isBusy: false);

        dialog.TurnSlider.Value.ShouldBe(AgentConfigurationDialog.MaximumTurns);
    }

    [Fact]
    public void Constructor_WhenBusy_DisablesEditingButKeepsCancel()
    {
        var dialog = new AgentConfigurationDialog(CodingAgentConfiguration.CreateDefault(), isBusy: true);

        dialog.ModelSelector.EffectiveIsEnabled.ShouldBeFalse();
        dialog.TurnSlider.EffectiveIsEnabled.ShouldBeFalse();
        dialog.ReasoningChoices.ShouldAllBe(static choice => !choice.EffectiveIsEnabled);
    }

    [Fact]
    public void Constructor_WhenConfigurationIsNull_Throws() =>
        Should.Throw<ArgumentNullException>(() => new AgentConfigurationDialog(null!, isBusy: false))
            .ParamName.ShouldBe("configuration");

    [Fact]
    public void SelectedReasoningEffort_WhenAnotherChoiceIsChecked_FollowsTheRadioGroup()
    {
        var dialog = new AgentConfigurationDialog(CodingAgentConfiguration.CreateDefault(), isBusy: false);

        dialog.ReasoningChoices[CodingAgentConfiguration.ReasoningEfforts.IndexOf(LlmReasoningEffort.Medium)].IsChecked = true;

        dialog.SelectedReasoningEffort.ShouldBe(LlmReasoningEffort.Medium);
    }

    [Theory]
    [InlineData(LlmReasoningEffort.Low, "quick")]
    [InlineData(LlmReasoningEffort.Medium, "balanced")]
    [InlineData(LlmReasoningEffort.High, "debugging")]
    [InlineData(LlmReasoningEffort.ExtraHigh, "hardest")]
    [InlineData(LlmReasoningEffort.None, "enables tools")]
    public void ReasoningLabel_WhenEffortIsKnown_ExplainsWorkload(
        LlmReasoningEffort effort,
        string expected) =>
        AgentConfigurationDialog.ReasoningLabel(effort).ShouldContain(expected);

    [Fact]
    public void ReasoningChoiceLabel_WhenAllEffortsAreCaptioned_UsesDistinctAccessKeys()
    {
        var keys = CodingAgentConfiguration.ReasoningEfforts
            .Select(AgentConfigurationDialog.ReasoningChoiceLabel)
            .Select(static label => char.ToUpperInvariant(label[label.IndexOf('&', StringComparison.Ordinal) + 1]))
            .ToArray();

        keys.Distinct().Count().ShouldBe(keys.Length);
    }

    [Theory]
    [InlineData(4, "Short")]
    [InlineData(12, "Balanced")]
    [InlineData(24, "Long")]
    [InlineData(40, "Very long")]
    public void TurnLimitDescription_WhenLimitFallsInABand_NamesTheBand(int turns, string expected) =>
        AgentConfigurationDialog.TurnLimitDescription(turns).ShouldStartWith(expected);
}
