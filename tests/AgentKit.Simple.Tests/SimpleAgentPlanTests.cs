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
    public void AdvertisedTools_WhenRegistrationsExist_ReturnsEveryDescriptorInOrder()
    {
        var registrations = new[] { Registration("a"), Registration("b") };

        var advertised = SimpleAgentPlan.AdvertisedTools(registrations);

        advertised.Select(static d => d.Id.Value).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void AdvertisedTools_WhenRegistrationsNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => SimpleAgentPlan.AdvertisedTools(null!)).ParamName.ShouldBe("registrations");

    private static RegisteredToolInvoker Registration(string id)
    {
        using var document = JsonDocument.Parse("{}");
        return new RegisteredToolInvoker(new ToolDescriptor(
            new ToolId(id),
            new ToolVersion("1"),
            id,
            id,
            new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), document.RootElement),
            null,
            new ToolEffects(ToolEffect.ReadOnly, null, null),
            new ToolExecutionHints(ToolSchedulingMode.Unspecified, null, null, null),
            new ToolSourceId("test"),
            ExtensionData.Empty));
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
    public void DefinitionFor_WhenAdditionalAgentIsConfigured_PublishesItsInstructionsAndModel()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var options = new SimpleAgentOptions();
        options.Instructions.Add("Be terse.");

        var definition = plan.DefinitionFor(new AgentId(Guid.NewGuid()), options);

        InstructionSourceProjection.ToMessages(definition.Instructions).ShouldHaveSingleItem().ShouldBeOfType<SystemMessage>().AgentId.ShouldBe(definition.Id);
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
    public void Apply_WhenCalled_PinsTheDefinitionAndConfigurationTheEngineAlsoPublishes()
    {
        // The engine's pinned AgentDefinition (from Definition()) and the conversation session's pinned definition
        // (from Apply()) are two projections of one plan, so they carry the exact same instruction identities.
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("assistant"), LocalDevelopmentDefaults = true };
        plan.Instructions.Add("Be concise.");
        plan.Instructions.Add("Cite sources.");

        var definition = plan.Definition();
        var options = new ConversationSessionOptions();
        plan.Apply(options);

        options.Agent.ShouldBe(definition);
        options.Configuration.ShouldNotBeNull().Version.ShouldBe(plan.ConfigurationVersion);
        options.Configuration.Fingerprint.ShouldBe(options.SessionProfile.ShouldNotBeNull().ConfigurationFingerprint);
    }

    [Fact]
    public void Definition_WhenCalledTwice_ReturnsTheSameInstructionIdentitiesBothTimes()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("assistant"), LocalDevelopmentDefaults = true };
        plan.Instructions.Add("Be concise.");

        var first = plan.Definition();
        var second = plan.Definition();

        first.Instructions.ShouldBe(second.Instructions);
    }

    [Fact]
    public void DefaultComponents_WhenRead_SelectsEveryFirstPartyDefaultKey()
    {
        var components = SimpleAgentPlan.DefaultComponents();

        components.Loop.ShouldBe(AgentLoopComponentDefaults.LoopKey);
        components.ContinuationPolicy.ShouldBe(AgentLoopComponentDefaults.ContinuationPolicyKey);
        components.Input.ShouldBe(AgentIOComponentDefaults.InputCoordinatorKey);
        components.Output.ShouldBe(AgentIOComponentDefaults.OutputPublisherKey);
        components.OutputProcessor.ShouldBe(AgentOutputComponentDefaults.ProcessorKey);
        components.Context.ShouldBe(AgentContextComponentDefaults.AssemblerKey);
        components.ModelSelector.ShouldBe(AgentProviderComponentDefaults.ModelSelectorKey);
        components.ModelExecutor.ShouldBe(AgentProviderComponentDefaults.ModelExecutorKey);
        components.BudgetProfile.ShouldBe(AgentBudgetComponentDefaults.ProfileKey);
    }

    [Fact]
    public void Definition_WhenNoOutputIsConfigured_SelectsTheFreeTextContract()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("assistant"), LocalDevelopmentDefaults = true };

        plan.Definition().Output.ShouldBeSameAs(OutputDefinition.FreeText);
    }

    [Fact]
    public void Definition_WhenRequestSettingsAreConfigured_CarriesThemOnTheModelPolicy()
    {
        var settings = LlmRequestSettings.Default with { Temperature = 0.4 };
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("assistant"), LocalDevelopmentDefaults = true, RequestSettings = settings };

        var definition = plan.Definition();

        definition.Models.RequestSettings.ShouldBe(settings);
        definition.Models.Requirements.ShouldBe(ModelRequirements.None);
        definition.Components.ShouldBe(SimpleAgentPlan.DefaultComponents());
        definition.HookProfile.ShouldBe(HookRegistrationDescriptors.DefaultProfileKey);
    }

    [Fact]
    public void DefinitionFor_WhenAdditionalAgentIsConfigured_SelectsTheSharedDefaultComponentsAndItsOwnOutput()
    {
        var plan = new SimpleAgentPlan { ModelAlias = new ModelAlias("chat"), LocalDevelopmentDefaults = true };
        var output = new OutputDefinition(
            new OutputDefinitionId("specialist"), new OutputDefinitionVersion("1"), "specialist", OutputMode.Prompted,
            schema: null, runtimeType: typeof(string), [], [], OutputValidationPolicy.RejectOnFirstFailure, OutputRetryPolicy.None,
            OutputEndStrategy.Graceful);

        var definition = plan.DefinitionFor(new AgentId(Guid.NewGuid()), new SimpleAgentOptions { Output = output });

        definition.Components.ShouldBe(SimpleAgentPlan.DefaultComponents());
        definition.Output.ShouldBe(output);
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
