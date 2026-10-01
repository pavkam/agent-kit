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

    /// <summary>Builds a valid options instance whose definition, configuration, and session profile agree, optionally customized further.</summary>
    /// <param name="configure">An optional callback that overrides individual options.</param>
    /// <returns>Options pinning an exact <see cref="AgentDefinition"/> and <see cref="EffectiveConfigurationSnapshot"/> pair.</returns>
    public static ConversationSessionOptions Valid(Action<ConversationSessionOptions>? configure = null)
    {
        var sessionProfile = TestSecurityEvidence.SessionProfile();
        // Matches TestSecurityEvidence.Authorization's hardcoded AgentDefinitionRevision(1) and security profile key,
        // which FakeSecurityProfileSelector returns by default for every captured authorization.
        var agent = AgentDefinitionFixtures.Create(
            AgentId,
            revision: 1,
            displayName: "agent",
            models: new ModelSelectionPolicy([new ModelAlias("test-model")]),
            securityProfile: new SecurityProfileKey("test-security"),
            sessionProfile: sessionProfile.Reference.Key);
        var options = new ConversationSessionOptions
        {
            Agent = agent,
            Configuration = new EffectiveConfigurationSnapshot(
                new ConfigurationVersion(1),
                sessionProfile.ConfigurationFingerprint,
                [],
                []),
            Identity = Identity,
            SessionProfile = sessionProfile,
        };
        configure?.Invoke(options);
        return options;
    }
}
