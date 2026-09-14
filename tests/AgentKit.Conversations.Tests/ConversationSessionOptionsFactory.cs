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
}
