// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>
/// The builder-time choices the <see cref="AgentEngineBuilderExtensions"/> sugar accumulates on one
/// <see cref="AgentEngineBuilder"/>, held as a singleton instance in its service collection so the
/// extension methods can chain in any order and the composition reads the finished plan lazily.
/// </summary>
/// <remarks>
/// Mutable only until the provider is built; every registration that consumes it resolves through DI,
/// which happens after the last <c>Use*</c>/<c>With*</c> call. Not thread-safe; a builder is single-threaded.
/// </remarks>
internal sealed class SimpleAgentPlan
{
    private static readonly AgentId _defaultAgentId = new(Guid.Parse("a9e0f3d2-5c1b-4e8a-9f6d-2b7c8d9e0f11"));

    /// <summary>Gets the instructions in call order.</summary>
    public List<string> Instructions { get; } = [];

    /// <summary>Gets or sets the selected model alias, when one was chosen.</summary>
    public ModelAlias? ModelAlias { get; set; }

    /// <summary>Gets or sets an explicitly supplied identity.</summary>
    public ExecutionIdentity? Identity { get; set; }

    /// <summary>Gets or sets an explicitly pinned agent identity.</summary>
    public AgentId? AgentId { get; set; }

    /// <summary>Gets or sets the per-run turn limit.</summary>
    public int MaxTurns { get; set; } = 12;

    /// <summary>Gets or sets the per-attempt timeout.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Gets or sets the portable request settings.</summary>
    public LlmRequestSettings RequestSettings { get; set; } = LlmRequestSettings.Default;

    /// <summary>Gets or sets a value indicating whether the named local-development defaults were opted into.</summary>
    public bool LocalDevelopmentDefaults { get; set; }

    /// <summary>Gets the agent definition revision every publication pins.</summary>
    public AgentDefinitionRevision DefinitionRevision { get; } = new(1);

    /// <summary>Gets the configuration version every publication pins.</summary>
    public ConfigurationVersion ConfigurationVersion { get; } = new(1);

    /// <summary>Gets the security profile key the definition selects.</summary>
    public SecurityProfileKey SecurityProfileKey { get; } = new("agentkit.simple.security");

    /// <summary>Gets the session profile key the definition selects.</summary>
    public SessionProfileKey SessionProfileKey { get; } = new("agentkit.simple.session");

    /// <summary>Gets the security authority key the standalone profile binds.</summary>
    public ComponentKey<ISecurityAuthority> AuthorityKey { get; } = new("agentkit.simple.authority");

    /// <summary>Gets the policy snapshot reference shared by the permission options and the security publication.</summary>
    public SecurityPolicySnapshotReference PolicySnapshot { get; } = new(
        new SecurityPolicySnapshotId(Guid.NewGuid()),
        new SecurityPolicyVersion(1),
        new ContentHash("sha256:agentkit-simple-policy:1"));

    /// <summary>Gets the effective agent identity.</summary>
    public AgentId EffectiveAgentId => AgentId ?? _defaultAgentId;

    /// <summary>Resolves the model alias or explains how to select one.</summary>
    /// <returns>The selected alias.</returns>
    /// <exception cref="InvalidOperationException">No model was selected.</exception>
    public ModelAlias RequireModelAlias() =>
        ModelAlias ?? throw new InvalidOperationException(
            "No model was selected. Call UseOpenAI, or register a provider on builder.Services and call UseModel.");

    /// <summary>Resolves the execution identity or explains how to supply one.</summary>
    /// <returns>The explicit identity, or the local-development identity when those defaults were opted into.</returns>
    /// <exception cref="InvalidOperationException">No identity was supplied and local defaults were not opted into.</exception>
    public ExecutionIdentity RequireIdentity() =>
        Identity
        ?? (LocalDevelopmentDefaults
            ? LocalDevelopmentIdentity()
            : throw new InvalidOperationException(
                "No identity was supplied. Call WithIdentity, or UseLocalDevelopmentDefaults for a local single-user agent."));

    /// <summary>Checks that every choice a run will need was made, so a gap fails at <c>Build()</c> rather than on the first message.</summary>
    /// <exception cref="InvalidOperationException">No model was selected or no identity is available.</exception>
    public void Validate()
    {
        _ = RequireModelAlias();
        _ = RequireIdentity();
    }

    /// <summary>Builds the security publication the engine, the selector, and the permission options all pin.</summary>
    /// <returns>The publication.</returns>
    public SecurityProfilePublication SecurityPublication() => new(
        EffectiveAgentId,
        DefinitionRevision,
        ConfigurationVersion,
        SecurityProfileKey,
        new SecurityProfileVersion(1),
        PolicySnapshot,
        AuthorityKey);

    /// <summary>Builds the session profile that selects the chosen store.</summary>
    /// <returns>An immutable profile; in-memory when local defaults are used, durable otherwise.</returns>
    public SessionProfileSnapshot SessionProfile() => new(
        new SessionProfileReference(SessionProfileKey, new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey(LocalDevelopmentDefaults ? "agentkit.in-memory" : "agentkit.sqlite"),
        SessionStoreCapabilities.Branching,
        requiresDurableStore: !LocalDevelopmentDefaults,
        requiresDistributedFencing: false,
        new SessionRetentionProfileKey("retention"),
        SessionBusyBehavior.Reject,
        maximumAppendEntries: 128,
        maximumPageSize: 256,
        verifySnapshotHashes: true,
        deleteOnDispose: false,
        new ContentHash(LocalDevelopmentDefaults ? "sha256:agentkit-simple-session-in-memory" : "sha256:agentkit-simple-session-durable"));

    /// <summary>Builds the exact run-profile publication the engine pins for the definition.</summary>
    /// <returns>The publication pairing the security and session profiles with the configuration snapshot.</returns>
    public AgentRunProfilePublication RunProfile()
    {
        var sessionProfile = SessionProfile();
        return new AgentRunProfilePublication(
            SecurityPublication(),
            sessionProfile,
            new EffectiveConfigurationSnapshot(ConfigurationVersion, sessionProfile.ConfigurationFingerprint, [], []));
    }

    /// <summary>Builds the agent definition the engine catalog publishes.</summary>
    /// <param name="tools">The tool definitions advertised to the model.</param>
    /// <returns>An immutable definition.</returns>
    public AgentDefinition Definition(ImmutableArray<LlmToolDefinition> tools) => new(
        EffectiveAgentId,
        DefinitionRevision,
        "agent",
        new ModelSelectionPolicy([RequireModelAlias()]),
        ModelRequirements.None,
        [.. Instructions.Select(InstructionMessage)],
        tools,
        LlmToolChoice.Auto,
        RequestSettings,
        new RunPolicyDefaults(MaxTurns, AttemptTimeout),
        ExtensionData.Empty,
        SecurityProfileKey,
        SessionProfileKey);

    /// <summary>Applies the plan to the conversation options.</summary>
    /// <param name="options">The options to populate.</param>
    public void Apply(ConversationSessionOptions options)
    {
        Debug.Assert(options is not null, "The options framework supplies the instance.");
        options.AgentId = EffectiveAgentId;
        options.Identity = RequireIdentity();
        options.SecurityProfileKey = SecurityProfileKey;
        options.AgentDefinitionRevision = DefinitionRevision;
        options.ConfigurationVersion = ConfigurationVersion;
        options.SessionProfile = SessionProfile();
        options.ModelSelectionPolicy = new ModelSelectionPolicy([RequireModelAlias()]);
        options.RequestSettings = RequestSettings;
        options.MaxTurns = MaxTurns;
        options.AttemptTimeout = AttemptTimeout;
        foreach (var instruction in Instructions)
        {
            options.Instructions.Add(InstructionMessage(instruction));
        }
    }

    private SystemMessage InstructionMessage(string text) => new(
        new MessageId(Guid.NewGuid()),
        EffectiveAgentId,
        default,
        null,
        default,
        null,
        null,
        DateTimeOffset.UtcNow,
        MessageState.Complete,
        [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);

    private static ExecutionIdentity LocalDevelopmentIdentity() => new(
        new TenantId("local"),
        new PrincipalId(Environment.UserName is { Length: > 0 } user ? user : "local-user"),
        ExecutionSubjectKind.Human,
        new AuthenticationEvidence(
            new AuthenticationEvidenceId("local-process"),
            new IdentityIssuerId("agentkit.simple"),
            "local-process",
            DateTimeOffset.UtcNow,
            null,
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:agentkit-simple-local-process"))),
        [],
        [],
        IdentityAssuranceLevel.Basic,
        new IdentityVersion(1));
}
