// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// Builds spec-shaped <see cref="AgentDefinition"/> values for tests: every required component key is the first-party
/// default key, every profile key is a stable test key, and nothing optional is enabled.
/// </summary>
public static class AgentDefinitionFixtures
{
    /// <summary>The model alias the default definition selects.</summary>
    public static ModelAlias DefaultAlias { get; } = new("chat");

    /// <summary>The security profile key the default definition selects.</summary>
    public static SecurityProfileKey SecurityProfile { get; } = new("security");

    /// <summary>The session profile key the default definition selects.</summary>
    public static SessionProfileKey SessionProfile { get; } = new("session");

    /// <summary>The hook profile key the default definition selects.</summary>
    public static HookProfileKey HookProfile { get; } = new("default");

    /// <summary>Gets the component selection naming each first-party default registration key and the default budget profile.</summary>
    /// <returns>A selection whose every key is the canonical default key.</returns>
    public static AgentComponentSelection DefaultComponents() => new(
        AgentLoopComponentDefaults.LoopKey,
        AgentLoopComponentDefaults.ContinuationPolicyKey,
        AgentIOComponentDefaults.InputCoordinatorKey,
        AgentIOComponentDefaults.OutputPublisherKey,
        AgentOutputComponentDefaults.ProcessorKey,
        AgentContextComponentDefaults.AssemblerKey,
        AgentProviderComponentDefaults.ModelSelectorKey,
        AgentProviderComponentDefaults.ModelExecutorKey,
        AgentBudgetComponentDefaults.ProfileKey);

    /// <summary>Builds a definition that selects only defaults.</summary>
    /// <param name="id">The agent identity; defaults to a fixed test identity.</param>
    /// <param name="revision">The content revision.</param>
    /// <param name="displayName">The diagnostic name.</param>
    /// <param name="maxTurns">The default turn limit.</param>
    /// <param name="models">The model policy; defaults to one candidate, <see cref="DefaultAlias"/>.</param>
    /// <param name="instructions">The instruction sources; defaults to none.</param>
    /// <param name="output">The output contract; defaults to <see cref="OutputDefinition.FreeText"/>.</param>
    /// <param name="components">The component selection; defaults to <see cref="DefaultComponents"/>.</param>
    /// <param name="optionalCapabilities">The optional capabilities; defaults to none.</param>
    /// <param name="toolsets">The toolset selections; defaults to none.</param>
    /// <param name="securityProfile">The security profile key; defaults to <see cref="SecurityProfile"/>.</param>
    /// <param name="sessionProfile">The session profile key; defaults to <see cref="SessionProfile"/>.</param>
    /// <returns>An immutable, valid definition.</returns>
    public static AgentDefinition Create(
        AgentId? id = null,
        long revision = 1,
        string displayName = "test agent",
        int maxTurns = 8,
        ModelSelectionPolicy? models = null,
        ImmutableArray<InstructionSource>? instructions = null,
        OutputDefinition? output = null,
        AgentComponentSelection? components = null,
        AgentOptionalCapabilitySelection? optionalCapabilities = null,
        ImmutableArray<ToolsetReference>? toolsets = null,
        SecurityProfileKey? securityProfile = null,
        SessionProfileKey? sessionProfile = null) => new(
            id ?? new AgentId(Guid.Parse("a0000000-0000-0000-0000-000000000001")),
            new AgentDefinitionRevision(revision),
            displayName,
            components ?? DefaultComponents(),
            sessionProfile ?? SessionProfile,
            HookProfile,
            securityProfile ?? SecurityProfile,
            optionalCapabilities ?? AgentOptionalCapabilitySelection.None,
            models ?? new ModelSelectionPolicy([DefaultAlias]),
            instructions ?? [],
            toolsets ?? [],
            new RunPolicyDefaults(maxTurns, TimeSpan.FromMinutes(1)),
            output ?? OutputDefinition.FreeText,
            ExtensionData.Empty);
}
