// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>Builds a fully valid, deterministic <see cref="ConversationSessionOptions"/> for tests, so each test only overrides what it cares about.</summary>
internal static class ConversationSessionOptionsFactory
{
    /// <summary>Gets the fixed agent every test session drives turns for.</summary>
    public static AgentId AgentId { get; } = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    /// <summary>Gets the fixed identity every test turn runs under.</summary>
    public static ExecutionIdentity Identity { get; } =
        TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);

    /// <summary>Builds a valid options instance, optionally customized further.</summary>
    public static ConversationSessionOptions Valid(Action<ConversationSessionOptions>? configure = null)
    {
        var options = new ConversationSessionOptions
        {
            AgentId = AgentId,
            Identity = Identity,
            SecurityProfileKey = new SecurityProfileKey("test-security"),
            ConfigurationVersion = new ConfigurationVersion(1),
            SessionProfile = TestSecurityEvidence.SessionProfile(),
            ModelSelectionPolicy = new ModelSelectionPolicy([new ModelAlias("test-model")]),
        };
        configure?.Invoke(options);
        return options;
    }

    /// <summary>Builds a valid options instance carrying an exact <see cref="AgentDefinition"/> and
    /// <see cref="EffectiveConfigurationSnapshot"/> pair whose evidence matches every decomposed option field.</summary>
    public static ConversationSessionOptions ValidWithExactEvidence(Action<ConversationSessionOptions>? configure = null)
    {
        var options = Valid();
        // Matches TestSecurityEvidence.Authorization's hardcoded AgentDefinitionRevision(1), which
        // FakeSecurityProfileSelector returns by default for every captured authorization.
        options.AgentDefinitionRevision = new AgentDefinitionRevision(1);
        options.Agent = new AgentDefinition(
            options.AgentId,
            options.AgentDefinitionRevision,
            "agent",
            options.ModelSelectionPolicy!,
            options.ModelRequirements,
            [],
            [],
            options.ToolChoice,
            options.RequestSettings,
            new RunPolicyDefaults(options.MaxTurns, options.AttemptTimeout),
            ExtensionData.Empty,
            options.SecurityProfileKey,
            options.SessionProfile!.Reference.Key);
        options.Configuration = new EffectiveConfigurationSnapshot(
            options.ConfigurationVersion,
            options.SessionProfile.ConfigurationFingerprint,
            [],
            []);
        configure?.Invoke(options);
        return options;
    }
}
