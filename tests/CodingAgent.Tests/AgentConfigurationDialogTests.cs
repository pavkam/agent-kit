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
        dialog.ReasoningSlider.Minimum.ShouldBe(0);
        dialog.ReasoningSlider.Maximum.ShouldBe(4);
        dialog.ReasoningSlider.Value.ShouldBe(CodingAgentConfiguration.ReasoningEfforts.IndexOf(LlmReasoningEffort.High));
        dialog.ReasoningSlider.Style.ShouldBeNull();
        dialog.HasSelectedResult.ShouldBeFalse();
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
}
