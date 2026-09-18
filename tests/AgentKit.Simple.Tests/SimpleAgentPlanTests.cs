// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

using AgentKit.Tools;

/// <summary>Verifies SimpleAgentPlan behavior and contracts.</summary>
public sealed class SimpleAgentPlanTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void SelectSugarModel_WhenMethodIsBlank_ThrowsArgumentException(string? method) =>
        Should.Throw<ArgumentException>(() => new SimpleAgentPlan().SelectSugarModel(method!)).ParamName.ShouldBe("method");

    [Fact]
    public void AdvertisedTools_WhenAllAreAllowed_ReturnsEveryDescriptorInOrder()
    {
        var tools = new ITool[] { new NamedTool("a"), new NamedTool("b") };

        var advertised = SimpleAgentPlan.AdvertisedTools(tools, new AgentToolsOptions { AllowAllRegisteredTools = true });

        advertised.Select(static d => d.Id.Value).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void AdvertisedTools_WhenAnAllowListIsSet_ReturnsOnlyListedDescriptors()
    {
        var tools = new ITool[] { new NamedTool("a"), new NamedTool("b"), new NamedTool("c") };
        var options = new AgentToolsOptions();
        _ = options.AllowedToolIds.Add(new ToolId("c"));
        _ = options.AllowedToolIds.Add(new ToolId("a"));

        var advertised = SimpleAgentPlan.AdvertisedTools(tools, options);

        advertised.Select(static d => d.Id.Value).ShouldBe(["a", "c"]);
    }

    [Fact]
    public void AdvertisedTools_WhenNothingIsAllowed_ReturnsEmpty() =>
        SimpleAgentPlan.AdvertisedTools([new NamedTool("a")], new AgentToolsOptions()).ShouldBeEmpty();

    [Fact]
    public void AdvertisedTools_WhenAnArgumentIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => SimpleAgentPlan.AdvertisedTools(null!, new AgentToolsOptions())).ParamName.ShouldBe("tools");
        Should.Throw<ArgumentNullException>(() => SimpleAgentPlan.AdvertisedTools([], null!)).ParamName.ShouldBe("toolOptions");
    }

    [Fact]
    public void AddAgent_WhenTheIdentityIsTheDefaultAgents_ThrowsInvalidOperationException()
    {
        var plan = new SimpleAgentPlan();

        _ = Should.Throw<InvalidOperationException>(() => plan.AddAgent(plan.EffectiveAgentId, new SimpleAgentOptions()));
    }

    [Fact]
    public void AddAgent_WhenOptionsIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SimpleAgentPlan().AddAgent(new AgentId(Guid.NewGuid()), null!)).ParamName.ShouldBe("options");

    [Fact]
    public void DefinitionFor_WhenToolsAreExcluded_PublishesNoToolsAndTheOutput()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var tools = SimpleAgentPlan.AdvertisedTools([new NamedTool("a")], new AgentToolsOptions { AllowAllRegisteredTools = true }).ToLlmToolDefinitions();
        var options = new SimpleAgentOptions { IncludeRegisteredTools = false };
        options.Instructions.Add("Be terse.");

        var definition = plan.DefinitionFor(new AgentId(Guid.NewGuid()), options, tools);

        definition.Tools.ShouldBeEmpty();
        definition.Instructions.ShouldHaveSingleItem().ShouldBeOfType<SystemMessage>().AgentId.ShouldBe(definition.Id);
        definition.Models.Candidates.ShouldBe([new ModelAlias("chat")]);
    }

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

    private sealed class NamedTool(string name): ITool
    {
        public ToolDescriptor Descriptor { get; } = new(
            new ToolId(name), new ToolVersion("1.0"), name, $"Tool {name}.",
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), JsonDocument.Parse("{}").RootElement),
            outputSchema: null,
            new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("test"), ExtensionData.Empty);

        public Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
