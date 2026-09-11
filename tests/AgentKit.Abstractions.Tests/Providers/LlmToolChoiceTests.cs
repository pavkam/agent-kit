// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies LlmToolChoice behavior and contracts.</summary>
public sealed class LlmToolChoiceTests
{
    [Fact]
    public void LlmToolChoice_Constructor_WhenNamedWithoutToolName_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new LlmToolChoice(LlmToolChoiceMode.Named, null));
    [Fact]
    public void LlmToolChoice_Constructor_WhenNotNamedWithToolName_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new LlmToolChoice(LlmToolChoiceMode.Auto, "tool"));
    [Fact]
    public void LlmToolChoice_Named_WhenToolNameInvalid_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => LlmToolChoice.Named(" "));
    [Fact]
    public void LlmToolChoice_Named_WhenValid_ProducesNamedChoice()
    {
        var choice = LlmToolChoice.Named("my-tool");
        choice.Mode.ShouldBe(LlmToolChoiceMode.Named);
        choice.ForcedToolName.ShouldBe("my-tool");
    }

    [Fact]
    public void LlmToolChoice_Equality_WhenSameValues_InstancesAreEqual() => LlmToolChoice.Named("tool").ShouldBe(LlmToolChoice.Named("tool"));
    [Fact]
    public void LlmToolChoice_SharedInstances_HaveExpectedModes()
    {
        LlmToolChoice.Auto.Mode.ShouldBe(LlmToolChoiceMode.Auto);
        LlmToolChoice.None.Mode.ShouldBe(LlmToolChoiceMode.None);
        LlmToolChoice.Required.Mode.ShouldBe(LlmToolChoiceMode.Required);
    }
}
