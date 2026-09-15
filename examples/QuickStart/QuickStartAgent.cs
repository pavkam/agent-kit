// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace QuickStart;

/// <summary>
/// Composes the smallest complete AgentKit agent: one OpenAI model, in-memory session and
/// security state, and no tools. Every registration below is a public DI extension; swap any
/// of them for a durable store, another provider, tool packages, or your own policies.
/// </summary>
internal static class QuickStartAgent
{
    /// <summary>Builds the composition and returns a conversation that owns the service provider.</summary>
    /// <param name="apiKey">The OpenAI API key. Credentials are never fabricated as defaults.</param>
    /// <returns>A conversation whose disposal also disposes the composition.</returns>
    public static OwnedConversationSession Create(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // Identities that pin this agent's configuration. Keep them stable across restarts so
        // persisted sessions and security evidence keep matching the agent that created them.
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var definitionRevision = new AgentDefinitionRevision(1);
        var configurationVersion = new ConfigurationVersion(1);
        var securityProfileKey = new SecurityProfileKey("quickstart-security");
        var authorityKey = new ComponentKey<ISecurityAuthority>("quickstart-authority");
        var identity = LocalIdentity();
        var alias = new ModelAlias("assistant");
        var modelId = new ModelId("gpt-4o-mini");

        var services = new ServiceCollection();

        // Security: a grant store, a standalone profile bound to this agent, and a policy.
        // AllowAll is for local single-tenant use only; production hosts register their own
        // ISecurityPolicy implementations and an audit sink with Required delivery.
        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddStandaloneSecurityProfile(
            agentId, definitionRevision, configurationVersion, securityProfileKey, authorityKey,
            configurePermissions: o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
        _ = services.AddAllowAllSecurityPolicy();

        // Session state: the coordinator plus an in-memory store and directory. Nothing here
        // survives the process; AgentKit.Session.Sqlite is the durable drop-in.
        _ = services.AddAgentSession();
        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionDirectory(new ComponentId("quickstart.session"));

        // The turn loop and its collaborators. No tool packages are registered yet.
        _ = services.AddAgentContext();
        _ = services.AddAgentOutput();
        _ = services.AddAgentLoop();
        _ = services.AddAgentTools();

        // The model: OpenAI, exposed to the agent under one alias. The bundled known-model catalog
        // supplies the model's published limits, capabilities, and list prices.
        _ = services.AddAgentProviders();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential(apiKey);
        _ = services.AddOpenAIKnownLlmModel(alias, modelId);

        // One conversation with one agent: session creation, message admission, and the
        // agent-loop run happen inside a single SendAsync call.
        _ = services.AddConversationSession(options =>
        {
            options.AgentId = agentId;
            options.Identity = identity;
            options.SecurityProfileKey = securityProfileKey;
            options.AgentDefinitionRevision = definitionRevision;
            options.ConfigurationVersion = configurationVersion;
            options.SessionProfile = InMemorySessionProfile();
            options.ModelSelectionPolicy = new ModelSelectionPolicy([alias]);
            options.Instructions.Add(SystemMessage(agentId, "You are a concise assistant."));
            options.MaxTurns = 4;
            options.AttemptTimeout = TimeSpan.FromMinutes(1);
        });

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        return new OwnedConversationSession(provider.GetRequiredService<IConversationSession>(), provider);
    }

    /// <summary>Describes the session profile that selects the in-memory store registered above.</summary>
    /// <returns>An immutable, non-durable session profile.</returns>
    private static SessionProfileSnapshot InMemorySessionProfile() => new(
        new SessionProfileReference(new SessionProfileKey("quickstart-session"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey("agentkit.in-memory"),
        SessionStoreCapabilities.Branching,
        requiresDurableStore: false,
        requiresDistributedFencing: false,
        new SessionRetentionProfileKey("retention"),
        SessionBusyBehavior.Reject,
        maximumAppendEntries: 128,
        maximumPageSize: 256,
        verifySnapshotHashes: true,
        deleteOnDispose: false,
        new ContentHash("sha256:quickstart-session-profile"));

    /// <summary>Creates the authenticated identity a local console user runs under.</summary>
    /// <returns>A basic-assurance human identity for the local tenant.</returns>
    private static ExecutionIdentity LocalIdentity() => new(
        new TenantId("local"),
        new PrincipalId("developer"),
        ExecutionSubjectKind.Human,
        new AuthenticationEvidence(
            new AuthenticationEvidenceId("local-console"),
            new IdentityIssuerId("quickstart"),
            "local-console",
            DateTimeOffset.UtcNow,
            null,
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:quickstart-console"))),
        [],
        [],
        IdentityAssuranceLevel.Basic,
        new IdentityVersion(1));

    /// <summary>Builds the agent's system instruction as an immutable message.</summary>
    /// <param name="agentId">The agent the instruction belongs to.</param>
    /// <param name="text">The instruction text.</param>
    /// <returns>A complete system message.</returns>
    private static SystemMessage SystemMessage(AgentId agentId, string text) => new(
        new MessageId(Guid.NewGuid()),
        agentId,
        default,
        null,
        default,
        null,
        null,
        DateTimeOffset.UtcNow,
        MessageState.Complete,
        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);
}
