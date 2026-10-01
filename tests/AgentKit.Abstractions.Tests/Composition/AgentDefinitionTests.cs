// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

using AgentKit.TestSupport;

/// <summary>Verifies AgentDefinition behavior and contracts.</summary>
public sealed class AgentDefinitionTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
    private static readonly ImmutableArray<InstructionSource> _sharedInstructions = Instructions();

    [Fact]
    public void Constructor_WhenAllSelectionsAreValid_PreservesEveryMember()
    {
        var components = AgentDefinitionFixtures.DefaultComponents();
        var optional = new AgentOptionalCapabilitySelection(new ComponentKey<IToolExecutor>("tools"), null, null, null, null, []);
        var models = new ModelSelectionPolicy([new ModelAlias("chat")]);
        var instructions = Instructions();
        ImmutableArray<ToolsetReference> toolsets = [new ToolsetReference(new ToolsetKey("set"), new ToolExecutionPolicyKey("policy"))];
        var runDefaults = new RunPolicyDefaults(4, TimeSpan.FromMinutes(2));
        var output = OutputDefinition.FreeText;

        var definition = new AgentDefinition(
            _agentId, new AgentDefinitionRevision(3), "agent", components, new SessionProfileKey("session"),
            new HookProfileKey("hooks"), new SecurityProfileKey("security"), optional, models, instructions,
            toolsets, runDefaults, output, ExtensionData.Empty);

        definition.Id.ShouldBe(_agentId);
        definition.Revision.ShouldBe(new AgentDefinitionRevision(3));
        definition.DisplayName.ShouldBe("agent");
        definition.Components.ShouldBe(components);
        definition.SessionProfile.ShouldBe(new SessionProfileKey("session"));
        definition.HookProfile.ShouldBe(new HookProfileKey("hooks"));
        definition.SecurityProfile.ShouldBe(new SecurityProfileKey("security"));
        definition.OptionalCapabilities.ShouldBe(optional);
        definition.Models.ShouldBe(models);
        definition.Instructions.ShouldBe(instructions);
        definition.Toolsets.ShouldBe(toolsets);
        definition.RunDefaults.ShouldBe(runDefaults);
        definition.Output.ShouldBe(output);
        definition.Extensions.ShouldBe(ExtensionData.Empty);
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(new Args { Id = default })).ParamName.ShouldBe("id");

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Constructor_WhenDisplayNameIsBlank_ThrowsExactParameter(string? displayName) =>
        Should.Throw<ArgumentException>(() => Build(new Args { DisplayName = displayName! })).ParamName.ShouldBe("displayName");

    [Fact]
    public void Constructor_WhenComponentsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => Build(new Args { Components = null! })).ParamName.ShouldBe("components");

    [Theory]
    [InlineData("session")]
    [InlineData("hook")]
    [InlineData("security")]
    public void Constructor_WhenProfileKeyIsDefault_ThrowsExactParameter(string which)
    {
        var exception = Should.Throw<ArgumentException>(() => Build(new Args
        {
            SessionProfile = which == "session" ? default : new SessionProfileKey("session"),
            HookProfile = which == "hook" ? default : new HookProfileKey("hooks"),
            SecurityProfile = which == "security" ? default : new SecurityProfileKey("security"),
        }));
        exception.ParamName.ShouldBe(which + "Profile");
    }

    [Fact]
    public void Constructor_WhenOptionalCapabilitiesIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => Build(new Args { OptionalCapabilities = null! })).ParamName.ShouldBe("optionalCapabilities");

    [Fact]
    public void Constructor_WhenModelsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => Build(new Args { Models = null! })).ParamName.ShouldBe("models");

    [Fact]
    public void Constructor_WhenInstructionsIsUninitialized_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Build(new Args { Instructions = default })).ParamName.ShouldBe("instructions");

    [Fact]
    public void Constructor_WhenInstructionsContainsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Build(new Args { Instructions = [null!] })).ParamName.ShouldBe("instructions");

    [Fact]
    public void Constructor_WhenToolsetsIsUninitialized_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Build(new Args { Toolsets = default })).ParamName.ShouldBe("toolsets");

    [Fact]
    public void Constructor_WhenToolsetsContainsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => Build(new Args { Toolsets = [null!] })).ParamName.ShouldBe("toolsets");

    [Fact]
    public void Constructor_WhenRunDefaultsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => Build(new Args { RunDefaults = null! })).ParamName.ShouldBe("runDefaults");

    [Fact]
    public void Constructor_WhenOutputIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => Build(new Args { Output = null! })).ParamName.ShouldBe("output");

    [Fact]
    public void Constructor_WhenExtensionsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => Build(new Args { Extensions = null! })).ParamName.ShouldBe("extensions");

    [Fact]
    public void With_WhenIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => _ = Valid() with { Id = default }).ParamName.ShouldBe("Id");

    [Fact]
    public void With_WhenDisplayNameIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => _ = Valid() with { DisplayName = " " }).ParamName.ShouldBe("DisplayName");

    [Fact]
    public void With_WhenComponentsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => _ = Valid() with { Components = null! }).ParamName.ShouldBe("Components");

    [Theory]
    [InlineData("SessionProfile")]
    [InlineData("HookProfile")]
    [InlineData("SecurityProfile")]
    public void With_WhenProfileKeyIsDefault_ThrowsExactParameter(string member)
    {
        var definition = Valid();
        var exception = Should.Throw<ArgumentException>(() => _ = member switch
        {
            "SessionProfile" => definition with { SessionProfile = default },
            "HookProfile" => definition with { HookProfile = default },
            _ => definition with { SecurityProfile = default },
        });
        exception.ParamName.ShouldBe(member);
    }

    [Fact]
    public void With_WhenOptionalCapabilitiesIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => _ = Valid() with { OptionalCapabilities = null! }).ParamName.ShouldBe("OptionalCapabilities");

    [Fact]
    public void With_WhenModelsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => _ = Valid() with { Models = null! }).ParamName.ShouldBe("Models");

    [Fact]
    public void With_WhenInstructionsContainsNull_ThrowsExactParameter()
    {
        ImmutableArray<InstructionSource> instructions = [null!];
        Should.Throw<ArgumentException>(() => _ = Valid() with { Instructions = instructions }).ParamName.ShouldBe("Instructions");
    }

    [Fact]
    public void With_WhenToolsetsIsUninitialized_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => _ = Valid() with { Toolsets = default }).ParamName.ShouldBe("Toolsets");

    [Fact]
    public void With_WhenRunDefaultsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => _ = Valid() with { RunDefaults = null! }).ParamName.ShouldBe("RunDefaults");

    [Fact]
    public void With_WhenOutputIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => _ = Valid() with { Output = null! }).ParamName.ShouldBe("Output");

    [Fact]
    public void With_WhenExtensionsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => _ = Valid() with { Extensions = null! }).ParamName.ShouldBe("Extensions");

    [Fact]
    public void With_WhenValuesAreValid_UpdatesEveryProperty()
    {
        var instructions = Instructions();
        var components = AgentDefinitionFixtures.DefaultComponents();
        var optional = new AgentOptionalCapabilitySelection(null, null, null, new MemoryProfileKey("memory"), null, []);
        ImmutableArray<ToolsetReference> toolsets = [new ToolsetReference(new ToolsetKey("set"), new ToolExecutionPolicyKey("policy"))];

        var changed = Valid() with
        {
            Revision = new AgentDefinitionRevision(9),
            DisplayName = "changed",
            Components = components,
            SessionProfile = new SessionProfileKey("other-session"),
            HookProfile = new HookProfileKey("other-hooks"),
            SecurityProfile = new SecurityProfileKey("other-security"),
            OptionalCapabilities = optional,
            Models = new ModelSelectionPolicy([new ModelAlias("other")]),
            Instructions = instructions,
            Toolsets = toolsets,
            RunDefaults = new RunPolicyDefaults(4, TimeSpan.FromMinutes(2)),
            Extensions = ExtensionData.Empty,
        };

        changed.Revision.ShouldBe(new AgentDefinitionRevision(9));
        changed.DisplayName.ShouldBe("changed");
        changed.SessionProfile.ShouldBe(new SessionProfileKey("other-session"));
        changed.HookProfile.ShouldBe(new HookProfileKey("other-hooks"));
        changed.SecurityProfile.ShouldBe(new SecurityProfileKey("other-security"));
        changed.OptionalCapabilities.ShouldBe(optional);
        changed.Instructions.ShouldBe(instructions);
        changed.Toolsets.ShouldBe(toolsets);
        changed.RunDefaults.MaxTurns.ShouldBe(4);
    }

    [Fact]
    public void Equality_WhenEveryMemberMatches_AreEqualAndHashEqually()
    {
        var left = Valid(instructions: _sharedInstructions);
        var right = Valid(instructions: _sharedInstructions);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equality_WhenAnyComponentKeyDiffers_AreNotEqual()
    {
        var components = AgentDefinitionFixtures.DefaultComponents();
        var other = new AgentComponentSelection(
            new ComponentKey<IAgentLoop>("other"), components.ContinuationPolicy, components.Input, components.Output,
            components.OutputProcessor, components.Context, components.ModelSelector, components.ModelExecutor,
            components.BudgetProfile);

        Valid().ShouldNotBe(Valid(components: other));
    }

    [Fact]
    public void Equality_WhenOutputDiffers_AreNotEqual()
    {
        var structured = new OutputDefinition(
            new OutputDefinitionId("structured"), new OutputDefinitionVersion("1"), "structured", OutputMode.Prompted,
            schema: null, runtimeType: typeof(string), [], [], OutputValidationPolicy.RejectOnFirstFailure,
            OutputRetryPolicy.None, OutputEndStrategy.Graceful);

        Valid().ShouldNotBe(Valid(output: structured));
    }

    [Fact]
    public void Equality_WhenModelRequirementsOrSettingsDiffer_AreNotEqual()
    {
        var requiring = new ModelSelectionPolicy([new ModelAlias("chat")], requirements: ModelRequirements.None with { RequiresReasoning = true });
        var sampling = new ModelSelectionPolicy([new ModelAlias("chat")], requestSettings: LlmRequestSettings.Default with { Temperature = 0.2 });

        Valid().ShouldNotBe(Valid(models: requiring));
        Valid().ShouldNotBe(Valid(models: sampling));
    }

    [Fact]
    public void Equality_WhenInstructionsOrToolsetsDiffer_AreNotEqual()
    {
        Valid().ShouldNotBe(Valid(instructions: Instructions()));
        Valid().ShouldNotBe(Valid(toolsets: [new ToolsetReference(new ToolsetKey("set"), new ToolExecutionPolicyKey("policy"))]));
    }

    [Fact]
    public void Equality_WhenOptionalCapabilitiesDiffer_AreNotEqual() =>
        Valid().ShouldNotBe(Valid(optionalCapabilities: new AgentOptionalCapabilitySelection(
            null, null, new DurabilityProfileKey("durable"), null, null, [])));

    private static ImmutableArray<InstructionSource> Instructions() =>
    [
        new LiteralInstructionSource(
            new ContextSourceReference(
                InstructionSourceProjection.DefinitionNamespace,
                new ContextSourceKey("primary"),
                new ContextSourceVersion("1")),
            ContextTrust.AgentDefinition,
            0,
            ContextScope.Agent,
            ContextEvaluationFrequency.OncePerModelRequest,
            [new SystemMessage(new MessageId(Guid.Parse("11111111-1111-1111-1111-111111111111")), _agentId, new SessionId(Guid.Parse("33333333-3333-3333-3333-333333333333")), null, new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [new TextPart("hi", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty)]),
    ];

    private static AgentDefinition Valid(
        ImmutableArray<InstructionSource>? instructions = null,
        AgentComponentSelection? components = null,
        OutputDefinition? output = null,
        ModelSelectionPolicy? models = null,
        ImmutableArray<ToolsetReference>? toolsets = null,
        AgentOptionalCapabilitySelection? optionalCapabilities = null) =>
        AgentDefinitionFixtures.Create(
            _agentId,
            instructions: instructions,
            components: components,
            output: output,
            models: models,
            toolsets: toolsets,
            optionalCapabilities: optionalCapabilities);

    private static AgentDefinition Build(Args args) => new(
        args.Id,
        new AgentDefinitionRevision(1),
        args.DisplayName,
        args.Components,
        args.SessionProfile,
        args.HookProfile,
        args.SecurityProfile,
        args.OptionalCapabilities,
        args.Models,
        args.Instructions,
        args.Toolsets,
        args.RunDefaults,
        args.Output,
        args.Extensions);

    /// <summary>Every constructor argument with a valid default, so one test overrides exactly one.</summary>
    private sealed record Args
    {
        public AgentId Id { get; init; } = _agentId;

        public string DisplayName { get; init; } = "agent";

        public AgentComponentSelection Components { get; init; } = AgentDefinitionFixtures.DefaultComponents();

        public SessionProfileKey SessionProfile { get; init; } = new("session");

        public HookProfileKey HookProfile { get; init; } = new("hooks");

        public SecurityProfileKey SecurityProfile { get; init; } = new("security");

        public AgentOptionalCapabilitySelection OptionalCapabilities { get; init; } = AgentOptionalCapabilitySelection.None;

        public ModelSelectionPolicy Models { get; init; } = new([new ModelAlias("chat")]);

        public ImmutableArray<InstructionSource> Instructions { get; init; } = [];

        public ImmutableArray<ToolsetReference> Toolsets { get; init; } = [];

        public RunPolicyDefaults RunDefaults { get; init; } = new(8, TimeSpan.FromMinutes(1));

        public OutputDefinition Output { get; init; } = OutputDefinition.FreeText;

        public ExtensionData Extensions { get; init; } = ExtensionData.Empty;
    }
}
