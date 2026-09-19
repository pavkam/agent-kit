// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;



/// <summary>Verifies AgentDefinition behavior and contracts.</summary>
public sealed class AgentDefinitionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AgentDefinition_WhenSelectedProfileIsBlank_ThrowsWithExactParameterName(bool securityProfile)
    {
        var security = securityProfile ? default : new SecurityProfileKey("security");
        var session = securityProfile ? new SessionProfileKey("session") : default;
        var exception = Should.Throw<ArgumentException>(() => Definition(security, session));
        exception.ParamName.ShouldBe(securityProfile ? "securityProfile" : "sessionProfile");
    }

    [Fact]
    public void AgentDefinition_WhenProfilesAreExplicit_PreservesSelections()
    {
        var security = new SecurityProfileKey("security");
        var session = new SessionProfileKey("session");
        var definition = Definition(security, session);
        definition.SecurityProfile.ShouldBe(security);
        definition.SessionProfile.ShouldBe(session);
    }

    [Fact]
    public void With_WhenIdIsDefault_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentOutOfRangeException>(() => _ = definition with { Id = default }).ParamName.ShouldBe("Id");
    }

    [Fact]
    public void With_WhenDisplayNameIsBlank_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentException>(() => _ = definition with { DisplayName = " " }).ParamName.ShouldBe("DisplayName");
    }

    [Fact]
    public void With_WhenModelsIsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentNullException>(() => _ = definition with { Models = null! }).ParamName.ShouldBe("Models");
    }

    [Fact]
    public void With_WhenModelRequirementsIsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentNullException>(() => _ = definition with { ModelRequirements = null! }).ParamName.ShouldBe("ModelRequirements");
    }

    [Fact]
    public void With_WhenInstructionsContainsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        ImmutableArray<AgentMessage> instructions = [null!];
        Should.Throw<ArgumentException>(() => _ = definition with { Instructions = instructions }).ParamName.ShouldBe("Instructions");
    }

    [Fact]
    public void With_WhenToolsContainsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        ImmutableArray<LlmToolDefinition> tools = [null!];
        Should.Throw<ArgumentException>(() => _ = definition with { Tools = tools }).ParamName.ShouldBe("Tools");
    }

    [Fact]
    public void With_WhenToolChoiceIsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentNullException>(() => _ = definition with { ToolChoice = null! }).ParamName.ShouldBe("ToolChoice");
    }

    [Fact]
    public void With_WhenSettingsIsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentNullException>(() => _ = definition with { Settings = null! }).ParamName.ShouldBe("Settings");
    }

    [Fact]
    public void With_WhenRunDefaultsIsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentNullException>(() => _ = definition with { RunDefaults = null! }).ParamName.ShouldBe("RunDefaults");
    }

    [Fact]
    public void With_WhenExtensionsIsNull_ThrowsExactParameter()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        Should.Throw<ArgumentNullException>(() => _ = definition with { Extensions = null! }).ParamName.ShouldBe("Extensions");
    }

    [Fact]
    public void With_WhenValuesAreValid_UpdatesEveryProperty()
    {
        var definition = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session"));
        var loopKey = new ComponentKey<IAgentLoop>("loop");
        var inputCoordinatorKey = new ComponentKey<IInputCoordinator>("input");
        var outputPublisherKey = new ComponentKey<IOutputPublisher>("output");
        var instructions = Instructions();
        var tools = Tools();
        var changed = definition with
        {
            LoopKey = loopKey,
            InputCoordinatorKey = inputCoordinatorKey,
            OutputPublisherKey = outputPublisherKey,
            DisplayName = "changed",
            Models = new ModelSelectionPolicy([new ModelAlias("other")]),
            ModelRequirements = ModelRequirements.None,
            Instructions = instructions,
            Tools = tools,
            ToolChoice = LlmToolChoice.None,
            Settings = LlmRequestSettings.Default,
            RunDefaults = new RunPolicyDefaults(4, TimeSpan.FromMinutes(2)),
            Extensions = ExtensionData.Empty,
        };
        changed.LoopKey.ShouldBe(loopKey);
        changed.InputCoordinatorKey.ShouldBe(inputCoordinatorKey);
        changed.OutputPublisherKey.ShouldBe(outputPublisherKey);
        changed.DisplayName.ShouldBe("changed");
        changed.ModelRequirements.ShouldBe(ModelRequirements.None);
        changed.Instructions.ShouldBe(instructions);
        changed.Tools.ShouldBe(tools);
        changed.ToolChoice.ShouldBe(LlmToolChoice.None);
        changed.Settings.ShouldBe(LlmRequestSettings.Default);
        changed.RunDefaults.MaxTurns.ShouldBe(4);
        changed.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Equality_WhenInputCoordinatorKeysDiffer_AreNotEqual()
    {
        var left = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session")) with
        {
            InputCoordinatorKey = new ComponentKey<IInputCoordinator>("a"),
        };
        var right = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session")) with
        {
            InputCoordinatorKey = new ComponentKey<IInputCoordinator>("b"),
        };

        left.ShouldNotBe(right);
    }

    [Fact]
    public void Equality_WhenOutputPublisherKeysDiffer_AreNotEqual()
    {
        var left = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session")) with
        {
            OutputPublisherKey = new ComponentKey<IOutputPublisher>("a"),
        };
        var right = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session")) with
        {
            OutputPublisherKey = new ComponentKey<IOutputPublisher>("b"),
        };

        left.ShouldNotBe(right);
    }

    [Fact]
    public void Equality_WhenInstructionsAndToolsAreNonEmpty_MatchesAndHashesEqually()
    {
        var left = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session")) with
        {
            Instructions = Instructions(),
            Tools = Tools(),
        };
        var right = Definition(new SecurityProfileKey("security"), new SessionProfileKey("session")) with
        {
            Instructions = Instructions(),
            Tools = Tools(),
        };
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    private static ImmutableArray<AgentMessage> Instructions() =>
        [new SystemMessage(new MessageId(Guid.Parse("11111111-1111-1111-1111-111111111111")), new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new SessionId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null, new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty)];

    private static ImmutableArray<LlmToolDefinition> Tools() => [new LlmToolDefinition(new ToolId("tool"), "tool", null, default)];

    private static AgentDefinition Definition(SecurityProfileKey securityProfile, SessionProfileKey sessionProfile) => new(new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")), new AgentDefinitionRevision(1), "agent", new ModelSelectionPolicy([new ModelAlias("chat")]), ModelRequirements.None, [], [], LlmToolChoice.Auto, LlmRequestSettings.Default, new RunPolicyDefaults(8, TimeSpan.FromMinutes(1)), ExtensionData.Empty, securityProfile, sessionProfile);
}
