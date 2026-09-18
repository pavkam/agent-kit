// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

/// <summary>Verifies SimpleAgentPlan behavior and contracts.</summary>
public sealed class SimpleAgentPlanTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void SelectSugarModel_WhenMethodIsBlank_ThrowsArgumentException(string? method) =>
        Should.Throw<ArgumentException>(() => new SimpleAgentPlan().SelectSugarModel(method!)).ParamName.ShouldBe("method");

    [Fact]
    public void SelectSugarModel_WhenFirstCalled_RecordsTheMethod()
    {
        var plan = new SimpleAgentPlan();

        plan.SelectSugarModel("UseOpenAI");

        plan.SugarProvider.ShouldBe("UseOpenAI");
    }

    [Fact]
    public void SelectSugarModel_WhenCalledAgain_ThrowsInvalidOperationExceptionNamingTheFirstAndKeepsIt()
    {
        var plan = new SimpleAgentPlan();
        plan.SelectSugarModel("UseOpenAI");

        var exception = Should.Throw<InvalidOperationException>(() => plan.SelectSugarModel("UseAnthropic"));

        exception.Message.ShouldContain("UseOpenAI");
        plan.SugarProvider.ShouldBe("UseOpenAI");
    }

    [Fact]
    public void Definition_AndApply_AgreeOnTheExactInstructionMessages()
    {
        // The engine's pinned AgentDefinition (from Definition()) and the conversation session's
        // actual sent instructions (from Apply()) are two projections of one plan; minting a fresh
        // MessageId/timestamp per call made them uncorrelated even though they describe "the same"
        // instruction.
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("assistant"), LocalDevelopmentDefaults = true };
        plan.Instructions.Add("Be concise.");
        plan.Instructions.Add("Cite sources.");

        var definition = plan.Definition([]);
        var options = new ConversationSessionOptions();
        plan.Apply(options);

        definition.Instructions.ShouldBe(options.Instructions);
    }

    [Fact]
    public void Definition_WhenCalledTwice_ReturnsTheSameInstructionIdentitiesBothTimes()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("assistant"), LocalDevelopmentDefaults = true };
        plan.Instructions.Add("Be concise.");

        var first = plan.Definition([]);
        var second = plan.Definition([]);

        first.Instructions.ShouldBe(second.Instructions);
    }

    [Fact]
    public void RequireIdentity_WhenCalledTwiceUnderLocalDevelopmentDefaults_ReturnsTheSameIdentity()
    {
        var plan = new SimpleAgentPlan { LocalDevelopmentDefaults = true };

        var first = plan.RequireIdentity();
        var second = plan.RequireIdentity();

        first.ShouldBe(second);
    }
}
