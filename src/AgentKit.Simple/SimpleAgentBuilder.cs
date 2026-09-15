// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>
/// A fluent builder for the shortest path to one working agent: it composes the real AgentKit loop,
/// context, output, session, security, tool, and provider packages on an ordinary
/// <see cref="IServiceCollection"/> and hands back a <see cref="SimpleAgent"/> that owns the result.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is a second runtime. Every <c>Use*</c>/<c>With*</c> call is sugar over the same public
/// registrations an application would write by hand, and <see cref="Services"/> is that collection, so
/// anything the sugar does not cover (tools, another provider, a durable store, your own policies) is
/// registered directly on it and the builder simply picks it up.
/// </para>
/// <para>
/// Storage, authority, and identity are external facts and are never chosen silently. Call
/// <see cref="UseLocalDevelopmentDefaults"/> to opt into in-memory state, an allow-all policy, and a
/// local identity by name, or register your own session, security, and identity services on
/// <see cref="Services"/> before <see cref="Build"/>. <see cref="Build"/> validates the whole graph and
/// fails with a named diagnostic when a required piece is missing.
/// </para>
/// <para>Instances are single-use and not thread-safe; build once.</para>
/// </remarks>
public sealed class SimpleAgentBuilder
{
    private static readonly ModelAlias _defaultAlias = new("assistant");

    private readonly List<string> _instructions = [];
    private ModelAlias? _modelAlias;
    private ExecutionIdentity? _identity;
    private AgentId? _agentId;
    private int _maxTurns = 12;
    private TimeSpan _attemptTimeout = TimeSpan.FromMinutes(3);
    private LlmRequestSettings _requestSettings = LlmRequestSettings.Default;
    private bool _localDefaults;
    private bool _built;

    private SimpleAgentBuilder() => Services = new ServiceCollection();

    /// <summary>Gets the service collection every registration lands on; register anything the sugar does not cover here.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Creates an empty builder.</summary>
    /// <returns>A builder with no services registered.</returns>
    public static SimpleAgentBuilder Create() => new();

    /// <summary>
    /// Opts into the defaults a local, single-user, single-process agent needs and nothing more: an
    /// in-memory session store and directory, an in-memory grant store, an allow-all security policy,
    /// best-effort audit delivery, every registered tool allowed, and a basic-assurance local identity.
    /// </summary>
    /// <returns>This builder.</returns>
    /// <remarks>
    /// This is named for what it is. Nothing survives the process, every request is permitted, and the
    /// identity is the process user. A service, multi-tenant host, or anything handling untrusted input
    /// registers its own session store, <see cref="ISecurityPolicy"/> implementations, audit sink, and
    /// authenticated identity on <see cref="Services"/> instead of calling this method.
    /// </remarks>
    public SimpleAgentBuilder UseLocalDevelopmentDefaults()
    {
        ThrowIfBuilt();
        _localDefaults = true;
        return this;
    }

    /// <summary>
    /// Uses one OpenAI model, registering the adapter, the API key, and a catalog descriptor whose limits,
    /// capabilities, and list prices come from the bundled <see cref="KnownModelCatalog"/>.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key. Never read from the environment implicitly.</param>
    /// <param name="modelId">OpenAI's model identifier, such as <c>"gpt-4o-mini"</c>; it must exist in <see cref="KnownModelCatalog.Default"/>.</param>
    /// <param name="configure">Optional OpenAI options, such as a different base address or non-streaming transport.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> or <paramref name="modelId"/> is blank, or the model is not a known OpenAI model.</exception>
    /// <remarks>
    /// Selects the model under the alias <c>assistant</c>. For a model the catalog does not know, or any
    /// other provider, register the provider's services on <see cref="Services"/> and call
    /// <see cref="UseModel"/> with the alias you registered.
    /// </remarks>
    public SimpleAgentBuilder UseOpenAI(string apiKey, string modelId, Action<OpenAIProviderOptions>? configure = null)
    {
        ThrowIfBuilt();
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        _ = Services.AddAgentProviders();
        _ = Services.AddOpenAI(configure);
        _ = Services.AddOpenAIApiKeyCredential(apiKey);
        _ = Services.AddOpenAIKnownLlmModel(_defaultAlias, new ModelId(modelId));
        _modelAlias = _defaultAlias;
        return this;
    }

    /// <summary>Selects the model alias the agent uses, for a provider registered directly on <see cref="Services"/>.</summary>
    /// <param name="alias">The alias under which the provider package registered the model.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="alias"/> is a default value.</exception>
    public SimpleAgentBuilder UseModel(ModelAlias alias)
    {
        ThrowIfBuilt();
        ArgumentException.ThrowIfNullOrWhiteSpace(alias.Value, nameof(alias));
        _ = Services.AddAgentProviders();
        _modelAlias = alias;
        return this;
    }

    /// <summary>Appends one system instruction. Call repeatedly to add several, in order.</summary>
    /// <param name="text">The instruction text.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    public SimpleAgentBuilder WithInstructions(string text)
    {
        ThrowIfBuilt();
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _instructions.Add(text);
        return this;
    }

    /// <summary>Uses an authenticated identity instead of the local-development default.</summary>
    /// <param name="identity">The identity every run executes under.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    public SimpleAgentBuilder WithIdentity(ExecutionIdentity identity)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(identity);
        _identity = identity;
        return this;
    }

    /// <summary>Pins the agent identity, so persisted sessions and security evidence keep matching across restarts.</summary>
    /// <param name="agentId">A stable, non-default agent identity.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is the default value.</exception>
    public SimpleAgentBuilder WithAgentId(AgentId agentId)
    {
        ThrowIfBuilt();
        ArgumentOutOfRangeException.ThrowIfEqual(agentId.Value, Guid.Empty, nameof(agentId));
        _agentId = agentId;
        return this;
    }

    /// <summary>Bounds one run to at most <paramref name="maxTurns"/> model turns.</summary>
    /// <param name="maxTurns">A positive turn limit.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTurns"/> is not positive.</exception>
    public SimpleAgentBuilder WithMaxTurns(int maxTurns)
    {
        ThrowIfBuilt();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
        _maxTurns = maxTurns;
        return this;
    }

    /// <summary>Bounds one model attempt to <paramref name="timeout"/>.</summary>
    /// <param name="timeout">A positive duration.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public SimpleAgentBuilder WithAttemptTimeout(TimeSpan timeout)
    {
        ThrowIfBuilt();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _attemptTimeout = timeout;
        return this;
    }

    /// <summary>Overrides the portable request settings (temperature, reasoning effort, and so on) sent with every attempt.</summary>
    /// <param name="settings">The settings to use.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
    public SimpleAgentBuilder WithRequestSettings(LlmRequestSettings settings)
    {
        ThrowIfBuilt();
        ArgumentNullException.ThrowIfNull(settings);
        _requestSettings = settings;
        return this;
    }

    /// <summary>
    /// Registers everything the selections above imply, builds and validates the service provider, and
    /// returns an agent that owns it.
    /// </summary>
    /// <returns>A ready agent; dispose it to release the composition.</returns>
    /// <exception cref="InvalidOperationException">
    /// No model was selected, <see cref="UseLocalDevelopmentDefaults"/> was not called and a required
    /// session, security, or identity registration is missing, or the builder was already built.
    /// </exception>
    public SimpleAgent Build()
    {
        ThrowIfBuilt();
        _built = true;

        var modelAlias = _modelAlias
            ?? throw new InvalidOperationException(
                $"No model was selected. Call {nameof(UseOpenAI)} or register a provider on {nameof(Services)} and call {nameof(UseModel)}.");

        var agentId = _agentId ?? new AgentId(Guid.Parse("a9e0f3d2-5c1b-4e8a-9f6d-2b7c8d9e0f11"));
        var definitionRevision = new AgentDefinitionRevision(1);
        var configurationVersion = new ConfigurationVersion(1);
        var securityProfileKey = new SecurityProfileKey("agentkit.simple.security");
        var authorityKey = new ComponentKey<ISecurityAuthority>("agentkit.simple.authority");
        var identity = _identity;

        if (_localDefaults)
        {
            identity ??= LocalDevelopmentIdentity();
            _ = Services.AddInMemorySecurityGrantStore();
            _ = Services.AddStandaloneSecurityProfile(
                agentId, definitionRevision, configurationVersion, securityProfileKey, authorityKey,
                configurePermissions: static o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
            _ = Services.AddAllowAllSecurityPolicy();
            _ = Services.AddInMemorySessionStore();
            _ = Services.AddInMemorySessionDirectory(new ComponentId("agentkit.simple.session"));
            _ = Services.AddAgentTools(static o => o.AllowAllRegisteredTools = true);
        }

        if (identity is null)
        {
            throw new InvalidOperationException(
                $"No identity was supplied. Call {nameof(WithIdentity)}, or {nameof(UseLocalDevelopmentDefaults)} for a local single-user agent.");
        }

        _ = Services.AddAgentSession();
        _ = Services.AddAgentContext();
        _ = Services.AddAgentOutput();
        _ = Services.AddAgentLoop();
        _ = Services.AddAgentTools();

        var sessionProfile = SessionProfile(_localDefaults);
        var instructions = _instructions.Select(text => Instruction(agentId, text)).ToArray();
        var capturedIdentity = identity;
        _ = Services.AddConversationSession(options =>
        {
            options.AgentId = agentId;
            options.Identity = capturedIdentity;
            options.SecurityProfileKey = securityProfileKey;
            options.AgentDefinitionRevision = definitionRevision;
            options.ConfigurationVersion = configurationVersion;
            options.SessionProfile = sessionProfile;
            options.ModelSelectionPolicy = new ModelSelectionPolicy([modelAlias]);
            options.RequestSettings = _requestSettings;
            options.MaxTurns = _maxTurns;
            options.AttemptTimeout = _attemptTimeout;
            foreach (var instruction in instructions)
            {
                options.Instructions.Add(instruction);
            }
        });

        // Every ITool registered on Services is advertised to the model, with its exact captured descriptor
        // bound for presentation. Resolved lazily when the options are first materialized.
        _ = Services.AddOptions<ConversationSessionOptions>()
            .Configure<IEnumerable<ITool>>(static (options, tools) =>
            {
                var descriptors = tools.Select(static tool => tool.Descriptor).ToImmutableArray();
                var definitions = descriptors.ToLlmToolDefinitions();
                for (var index = 0; index < descriptors.Length; index++)
                {
                    options.Tools.Add(definitions[index]);
                    options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(descriptors[index], definitions[index]));
                }
            });

        ServiceProvider provider;
        try
        {
            provider = Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }
        catch (AggregateException exception) when (!_localDefaults)
        {
            throw new InvalidOperationException(
                $"The agent's services could not be composed. Either call {nameof(UseLocalDevelopmentDefaults)} or register a session store and directory, "
                + $"a security grant store, profile and policy on {nameof(Services)}. See the inner exception for the first missing service.",
                exception.InnerExceptions.Count > 0 ? exception.InnerExceptions[0] : exception);
        }

        try
        {
            return new SimpleAgent(provider.GetRequiredService<IConversationSession>(), provider);
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    private void ThrowIfBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException("The builder has already been built; create a new builder for another agent.");
        }
    }

    private static SessionProfileSnapshot SessionProfile(bool inMemory) => new(
        new SessionProfileReference(new SessionProfileKey("agentkit.simple.session"), new SessionProfileVersion(1)),
        new ComponentKey<ISessionCoordinator>("coordinator"),
        new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
        new SessionStoreKey(inMemory ? "agentkit.in-memory" : "agentkit.sqlite"),
        SessionStoreCapabilities.Branching,
        requiresDurableStore: !inMemory,
        requiresDistributedFencing: false,
        new SessionRetentionProfileKey("retention"),
        SessionBusyBehavior.Reject,
        maximumAppendEntries: 128,
        maximumPageSize: 256,
        verifySnapshotHashes: true,
        deleteOnDispose: false,
        new ContentHash(inMemory ? "sha256:agentkit-simple-session-in-memory" : "sha256:agentkit-simple-session-durable"));

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

    private static SystemMessage Instruction(AgentId agentId, string text)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(text), "WithInstructions validates the text before it is stored.");
        return new SystemMessage(
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
}
