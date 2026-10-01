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

    /// <summary>The memoized instruction messages, built once from <see cref="Instructions"/> the first time
    /// either <see cref="Definition"/> or <see cref="Apply"/> needs them.</summary>
    /// <remarks>
    /// Both call sites must observe the exact same identity, timestamp, and content for "the same"
    /// instruction: the engine's pinned <see cref="AgentDefinition"/> and the conversation session's
    /// actual sent instructions are two projections of one plan, and minting a fresh
    /// <see cref="MessageId"/>/<see cref="DateTimeOffset.UtcNow"/> per call (the prior behavior) made
    /// them uncorrelated and non-deterministic. Only one memoization is required because, per this
    /// type's own documented lifecycle, both call sites run after every <c>Use*</c>/<c>With*</c> call
    /// has already finished mutating <see cref="Instructions"/>.
    /// </remarks>
    private ImmutableArray<AgentMessage>? _instructionMessages;

    /// <summary>The memoized local-development identity, built once so every caller within one plan observes
    /// the same identity and authentication timestamp instead of a fresh one per call.</summary>
    private ExecutionIdentity? _localDevelopmentIdentity;

    /// <summary>Gets the instructions in call order.</summary>
    public List<string> Instructions { get; } = [];

    /// <summary>Gets or sets the selected model alias, when one was chosen.</summary>
    public ModelAlias? ModelAlias { get; set; }

    /// <summary>
    /// Gets the name of the <c>Use&lt;Provider&gt;</c> method that registered a model under the shared sugar alias,
    /// or <see langword="null"/> when none has. Every sugar method uses the same alias, so a second one would publish
    /// a duplicate descriptor; this lets the second call fail immediately instead of at the first turn.
    /// </summary>
    public string? SugarProvider { get; private set; }

    /// <summary>
    /// Records that <paramref name="method"/> selected the shared sugar alias, rejecting a second selection.
    /// </summary>
    /// <param name="method">The sugar method's name, used in the diagnostic.</param>
    /// <exception cref="ArgumentException"><paramref name="method"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">Another sugar method already selected a model.</exception>
    public void SelectSugarModel(string method)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (SugarProvider is { } existing)
        {
            throw new InvalidOperationException(
                $"{existing} already selected the model; a builder selects one model through the Use<Provider> methods. " +
                "Register further providers on Services and pick one with UseModel.");
        }

        SugarProvider = method;
    }

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

    /// <summary>Gets or sets the structured-output contract every turn must satisfy, or <see langword="null"/> for free text.</summary>
    public OutputDefinition? Output { get; set; }

    /// <summary>Gets or sets the budget limits every run of the default agent reserves against; empty for none.</summary>
    public ImmutableArray<BudgetLimit> BudgetLimits { get; set; } = [];

    /// <summary>Gets the additional agents hosted next to the default one, keyed by their pinned identities.</summary>
    public Dictionary<AgentId, SimpleAgentOptions> AdditionalAgents { get; } = [];

    /// <summary>Gets the authored toolset keys this plan selects for run-bound discovery.</summary>
    public List<ToolsetKey> ToolsetKeys { get; } = [];

    /// <summary>
    /// Records an additional agent, rejecting an identity already used by the default agent or another addition.
    /// </summary>
    /// <param name="agentId">The additional agent's stable identity.</param>
    /// <param name="options">Its validated behavior.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The identity is already hosted by this plan.</exception>
    public void AddAgent(AgentId agentId, SimpleAgentOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentNullException.ThrowIfNull(options);
        if (agentId == EffectiveAgentId || AdditionalAgents.ContainsKey(agentId))
        {
            throw new InvalidOperationException($"Agent {agentId} is already hosted by this engine; every agent needs a distinct identity.");
        }

        AdditionalAgents.Add(agentId, options);
    }

    /// <summary>Gets or sets a value indicating whether the named local-development defaults were opted into.</summary>
    public bool LocalDevelopmentDefaults { get; set; }

    /// <summary>Gets or sets a value indicating whether sessions were pointed at the durable SQLite store.</summary>
    public bool DurableSessions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every hosted definition selects this plan's durability profile, so
    /// each first-party boundary runs as a recoverable operation.
    /// </summary>
    public bool DurableExecution { get; set; }

    /// <summary>
    /// Gets or sets the storage the durability sugar selected (<c>in-memory</c>, <c>sqlite</c>, or <c>json</c>), or
    /// <see langword="null"/> when durability was not requested.
    /// </summary>
    /// <value>
    /// One explicit choice per plan: selecting a second, different store is refused so a composition never mixes journals.
    /// </value>
    public string? DurabilityStorage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every hosted definition selects this plan's goal profile, so each
    /// agent owns goals and may delegate to its peers.
    /// </summary>
    public bool Delegation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every hosted definition selects this plan's memory profile, so each
    /// agent can retain and retrieve durable memory.
    /// </summary>
    public bool Memory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every hosted definition selects this plan's compaction profile, so each
    /// agent compacts under the profile's policy and compactor.
    /// </summary>
    public bool Compaction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every hosted definition selects this plan's artifact coordinator, so each
    /// agent can persist and read durable binary content through the keyed coordinator.
    /// </summary>
    public bool Artifacts { get; set; }

    /// <summary>Gets a value indicating whether the session profile selects the in-memory store.</summary>
    public bool InMemorySessions => LocalDevelopmentDefaults && !DurableSessions;

    /// <summary>Gets the agent definition revision every publication pins.</summary>
    public AgentDefinitionRevision DefinitionRevision { get; } = new(1);

    /// <summary>Gets the configuration version every publication pins.</summary>
    public ConfigurationVersion ConfigurationVersion { get; } = new(1);

    /// <summary>Gets the security profile key the definition selects.</summary>
    public SecurityProfileKey SecurityProfileKey { get; } = new("agentkit.simple.security");

    /// <summary>Gets the session profile key the definition selects.</summary>
    public SessionProfileKey SessionProfileKey { get; } = new("agentkit.simple.session");

    /// <summary>Gets the durability profile key every definition selects once <see cref="DurableExecution"/> is set.</summary>
    /// <value>
    /// The single key the sugar registers its journal, lease manager, recovery policy, and backend selection under.
    /// A composition needing more than one durable component selection registers its own profiles on the service
    /// collection and publishes its own definitions.
    /// </value>
    public DurabilityProfileKey DurabilityProfileKey { get; } = new("agentkit.simple.durability");

    /// <summary>Gets the goal profile key every definition selects once <see cref="Delegation"/> is set.</summary>
    /// <value>The single key the sugar registers its goal store, dispatcher, and limits under.</value>
    public GoalProfileKey GoalProfileKey { get; } = new("agentkit.simple.delegation");

    /// <summary>Gets the version of the goal profile the sugar publishes.</summary>
    public GoalProfileVersion GoalProfileVersion { get; } = new(1);

    /// <summary>Gets the memory profile key every definition selects once <see cref="Memory"/> is set.</summary>
    /// <value>The single key the sugar registers its memory store, retrieval source, and ceilings under.</value>
    public MemoryProfileKey MemoryProfileKey { get; } = new("agentkit.simple.memory");

    /// <summary>Gets the compaction profile key every definition selects once <see cref="Compaction"/> is set.</summary>
    /// <value>The single key the sugar registers its extractive profile under, bound to the default compactor key.</value>
    public CompactionProfileKey CompactionProfileKey { get; } = new("agentkit.simple.compaction");

    /// <summary>Gets the artifact coordinator key every definition selects once <see cref="Artifacts"/> is set.</summary>
    /// <value>The single key the sugar registers its coordinator under.</value>
    public ComponentKey<IArtifactCoordinator> ArtifactCoordinatorKey { get; } = new("agentkit.simple.artifacts");

    /// <summary>Gets the artifact profile key the sugar's coordinator is bound to.</summary>
    public ArtifactProfileKey ArtifactProfileKey { get; } = new("agentkit.simple.artifacts");

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
            ? _localDevelopmentIdentity ??= LocalDevelopmentIdentity()
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
    public SecurityProfilePublication SecurityPublication() => SecurityPublication(EffectiveAgentId);

    /// <summary>Builds the security publication for one hosted agent; every agent shares the plan's profile, policy, and authority.</summary>
    /// <param name="agentId">The hosted agent.</param>
    /// <returns>The publication the engine pins and the security authority reads for that agent.</returns>
    public SecurityProfilePublication SecurityPublication(AgentId agentId) => new(
        agentId,
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
        new SessionStoreKey(InMemorySessions ? "agentkit.in-memory" : "agentkit.sqlite"),
        SessionStoreCapabilities.Branching,
        requiresDurableStore: !InMemorySessions,
        requiresDistributedFencing: false,
        new SessionRetentionProfileKey("retention"),
        SessionBusyBehavior.Reject,
        maximumAppendEntries: 128,
        maximumPageSize: 256,
        verifySnapshotHashes: true,
        deleteOnDispose: false,
        new ContentHash(InMemorySessions ? "sha256:agentkit-simple-session-in-memory" : "sha256:agentkit-simple-session-durable"));

    /// <summary>Builds the exact run-profile publication the engine pins for the definition.</summary>
    /// <returns>The publication pairing the security and session profiles with the configuration snapshot.</returns>
    public AgentRunProfilePublication RunProfile() => RunProfile(EffectiveAgentId);

    /// <summary>Builds the run-profile publication for one hosted agent over the plan's shared session profile.</summary>
    /// <param name="agentId">The hosted agent.</param>
    /// <returns>The publication the engine pins for that agent.</returns>
    public AgentRunProfilePublication RunProfile(AgentId agentId)
    {
        var sessionProfile = SessionProfile();
        return new AgentRunProfilePublication(
            SecurityPublication(agentId),
            sessionProfile,
            HookRegistrationDescriptors.DefaultProfileKey,
            AgentBudgetComponentDefaults.ProfileKey,
            new EffectiveConfigurationSnapshot(ConfigurationVersion, sessionProfile.ConfigurationFingerprint, [], []));
    }

    /// <summary>Builds the agent definition the engine catalog publishes.</summary>
    /// <returns>An immutable definition.</returns>
    public AgentDefinition Definition() => BuildDefinition(
        EffectiveAgentId,
        "agent",
        InstructionMessages(),
        RequestSettings,
        MaxTurns,
        AttemptTimeout,
        Output,
        AuthoredToolsets(),
        OptionalCapabilities(includeTools: true));

    /// <summary>Applies the plan to the conversation options.</summary>
    /// <param name="options">The options to populate.</param>
    public void Apply(ConversationSessionOptions options)
    {
        Debug.Assert(options is not null, "The options framework supplies the instance.");
        var sessionProfile = SessionProfile();
        options.Agent = Definition();
        options.Configuration = new EffectiveConfigurationSnapshot(ConfigurationVersion, sessionProfile.ConfigurationFingerprint, [], []);
        options.Identity = RequireIdentity();
        options.SessionProfile = sessionProfile;
    }

    /// <summary>Selects every application-registered tool descriptor for conversation presentation bindings.</summary>
    /// <param name="registrations">Every invoker registration marker on the service collection.</param>
    /// <returns>The advertised descriptors, in registration order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registrations"/> is null.</exception>
    public static ImmutableArray<ToolDescriptor> AdvertisedTools(IEnumerable<RegisteredToolInvoker> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        return [.. registrations.Select(static registration => registration.Descriptor)];
    }

    /// <summary>Builds the immutable definition of one additional agent over the plan's shared model and profiles.</summary>
    /// <param name="agentId">The additional agent's identity.</param>
    /// <param name="options">Its configured behavior.</param>
    /// <returns>The definition the engine catalog publishes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public AgentDefinition DefinitionFor(AgentId agentId, SimpleAgentOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentNullException.ThrowIfNull(options);
        return BuildDefinition(
            agentId,
            options.DisplayName,
            [.. options.Instructions.Select(text => BuildInstructionMessage(agentId, text)).Cast<AgentMessage>()],
            options.RequestSettings,
            options.MaxTurns,
            options.AttemptTimeout,
            options.Output,
            options.IncludeRegisteredTools ? AuthoredToolsets() : [],
            OptionalCapabilities(options.IncludeRegisteredTools));
    }

    /// <summary>Builds the keyed component selection every hosted definition shares: the first-party defaults.</summary>
    /// <returns>One selection naming each first-party default registration and the default budget profile.</returns>
    internal static AgentComponentSelection DefaultComponents() => new(
        AgentLoopComponentDefaults.LoopKey,
        AgentLoopComponentDefaults.ContinuationPolicyKey,
        AgentIOComponentDefaults.InputCoordinatorKey,
        AgentIOComponentDefaults.OutputPublisherKey,
        AgentOutputComponentDefaults.ProcessorKey,
        AgentContextComponentDefaults.AssemblerKey,
        AgentProviderComponentDefaults.ModelSelectorKey,
        AgentProviderComponentDefaults.ModelExecutorKey,
        AgentBudgetComponentDefaults.ProfileKey);

    private AgentDefinition BuildDefinition(
        AgentId agentId,
        string displayName,
        ImmutableArray<AgentMessage> instructions,
        LlmRequestSettings requestSettings,
        int maxTurns,
        TimeSpan attemptTimeout,
        OutputDefinition? output,
        ImmutableArray<ToolsetReference> toolsets,
        AgentOptionalCapabilitySelection optionalCapabilities) => new(
            agentId,
            DefinitionRevision,
            displayName,
            DefaultComponents(),
            SessionProfileKey,
            HookRegistrationDescriptors.DefaultProfileKey,
            SecurityProfileKey,
            optionalCapabilities,
            new ModelSelectionPolicy([RequireModelAlias()], requestSettings: requestSettings),
            InstructionSourceProjection.FromMessages(instructions, DefinitionRevision),
            toolsets,
            new RunPolicyDefaults(maxTurns, attemptTimeout),
            output ?? OutputDefinition.FreeText,
            ExtensionData.Empty);

    /// <summary>Builds immutable toolset references from the plan's authored keys.</summary>
    internal ImmutableArray<ToolsetReference> AuthoredToolsets() =>
    [
        .. ToolsetKeys
            .Distinct()
            .Select(static key => new ToolsetReference(key, SimpleToolRuntime.StandardExecutionPolicyKey)),
    ];

    /// <summary>Resolves the optional capabilities one hosted definition selects.</summary>
    /// <param name="includeTools">
    /// Whether this definition is advertised the plan's registered tools. A specialist agent configured without
    /// them selects no tool executor, but still selects durability: whether work is recoverable is a property of
    /// the engine's composition, not of whether that agent happens to call tools.
    /// </param>
    /// <returns>The selection, which is <see cref="AgentOptionalCapabilitySelection.None"/> when neither applies.</returns>
    internal AgentOptionalCapabilitySelection OptionalCapabilities(bool includeTools)
    {
        var toolExecutor = includeTools && ToolsetKeys.Count > 0
            ? SimpleToolRuntime.ToolExecutorKey
            : (ComponentKey<IToolExecutor>?) null;
        var durabilityProfile = DurableExecution ? DurabilityProfileKey : (DurabilityProfileKey?) null;
        var goalProfile = Delegation ? GoalProfileKey : (GoalProfileKey?) null;
        var memoryProfile = Memory ? MemoryProfileKey : (MemoryProfileKey?) null;
        var artifactCoordinator = Artifacts ? ArtifactCoordinatorKey : (ComponentKey<IArtifactCoordinator>?) null;
        var compactionProfile = Compaction ? CompactionProfileKey : (CompactionProfileKey?) null;
        return toolExecutor is null && artifactCoordinator is null && durabilityProfile is null && goalProfile is null
            && memoryProfile is null && compactionProfile is null
            ? AgentOptionalCapabilitySelection.None
            : new AgentOptionalCapabilitySelection(toolExecutor, artifactCoordinator, durabilityProfile, memoryProfile, goalProfile, [])
            {
                CompactionProfile = compactionProfile,
            };
    }

    /// <summary>Builds, or returns the already-built, exact instruction messages for this plan.</summary>
    /// <returns>One immutable message per entry in <see cref="Instructions"/>, in call order.</returns>
    private ImmutableArray<AgentMessage> InstructionMessages() =>
        _instructionMessages ??= [.. Instructions.Select(BuildInstructionMessage).Cast<AgentMessage>()];

    private SystemMessage BuildInstructionMessage(string text) => BuildInstructionMessage(EffectiveAgentId, text);

    private static SystemMessage BuildInstructionMessage(AgentId agentId, string text) => new(
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

    private static ExecutionIdentity LocalDevelopmentIdentity() => ExecutionIdentity.ForHuman(
        new TenantId("local"),
        new PrincipalId(Environment.UserName is { Length: > 0 } user ? user : "local-user"),
        new IdentityIssuerId("agentkit.simple"),
        "local-process",
        DateTimeOffset.UtcNow);
}
