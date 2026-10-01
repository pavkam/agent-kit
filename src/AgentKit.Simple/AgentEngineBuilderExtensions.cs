// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

using AgentKit.Durability.Sqlite;
using AgentKit.Providers;
using AgentKit.Storage.Json;

using SqliteDatabaseOpenMode = SqliteDatabaseOpenMode;
using SqliteSchemaMode = SqliteSchemaMode;

/// <summary>
/// The shortest path to a working agent on the real <see cref="AgentEngineBuilder"/>: fluent <c>Use*</c> and
/// <c>With*</c> calls that compose the loop, context, output, session, security, tool, and provider packages
/// through their ordinary public registrations, publish one agent definition to the engine catalog, and add
/// one conversation the built engine answers through <see cref="AgentEngineExtensions.AskAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is a second runtime. Every call is sugar over registrations an application could write on
/// <see cref="AgentEngineBuilder.Services"/> itself, and that collection remains the escape hatch: tools,
/// another provider, a durable store, or your own security policies are registered there and picked up.
/// Calls may appear in any order before <see cref="AgentEngineBuilder.Build"/>; the finished plan is read
/// lazily when the provider is built.
/// </para>
/// <para>
/// Storage, authority, and identity are external facts and are never chosen silently.
/// <see cref="UseLocalDevelopmentDefaults"/> opts into in-memory state, an allow-all policy, and a local
/// identity by name; otherwise register your own session, security, and identity services and
/// <see cref="AgentEngineBuilder.Build"/> fails with a diagnostic that names what is missing.
/// </para>
/// </remarks>
public static class AgentEngineBuilderExtensions
{
    /// <summary>The alias every <c>Use&lt;Provider&gt;</c> method registers its model under.</summary>
    internal static ModelAlias DefaultAlias { get; } = new("assistant");

    /// <summary>The descriptor source the <c>Use&lt;Provider&gt;</c> methods publish an explicitly described model through.</summary>
    internal static ModelDescriptorSourceId DescriptorSourceId { get; } = new("agentkit.simple.model");

    /// <summary>
    /// The placeholder <see cref="UseOllama"/> sends when the caller supplies no key. A local Ollama server ignores
    /// the <c>Authorization</c> header, but the OpenAI-compatible transport requires one to be configured.
    /// </summary>
    internal const string OllamaPlaceholderApiKey = "ollama";

    /// <summary>The store identity <see cref="UseSqliteSessions"/> stamps into database files when the caller supplies none.</summary>
    internal static SqliteSessionStoreInstanceId DefaultSqliteInstanceId { get; } = new(Guid.Parse("5e1f0a9c-3b2d-4c7e-8f10-a1b2c3d4e5f6"));

    internal static SqliteDurableStoreInstanceId DefaultSqliteDurabilityInstanceId { get; } = new(Guid.Parse("7a3c1e52-9d44-4b8f-a0c6-2f5e8d1b3a70"));

    internal static JsonDurableStoreInstanceId DefaultJsonDurabilityInstanceId { get; } = new(Guid.Parse("c4d91b07-6e28-4f3a-b5d2-90a7e1c8f634"));

    /// <summary>Every first-party recoverable operation name the durability sugar enables and permits its backend to own.</summary>
    /// <remarks>
    /// The sugar enables all of them because it configures one profile for one composition: an application that
    /// wants to journal only some boundaries registers its own profile with the exact names it wants.
    /// </remarks>
    internal static ImmutableArray<DurableOperationName> FirstPartyOperations { get; } =
    [
        .. LoopDurableOperations.All,
        .. IoDurableOperations.All,
        .. CompactionDurableOperations.All,
        .. PermissionsDurableOperations.All,
        .. EngineDurableOperations.All,
    ];

    private static AgentEngineBuilder ConfigureDurability(
        AgentEngineBuilder builder,
        Action<AgentDurabilityOptions>? configure,
        DurabilityStorageKind storage,
        string? storagePath,
        SqliteDurableStoreInstanceId? sqliteInstanceId,
        JsonDurableStoreInstanceId? jsonInstanceId)
    {
        Debug.Assert(builder is not null, "Public entry points validate the builder.");
        var plan = Plan(builder);
        var name = storage switch
        {
            DurabilityStorageKind.InMemory => "in-memory",
            DurabilityStorageKind.Sqlite => "sqlite",
            DurabilityStorageKind.Json => "json",
            _ => throw new ArgumentOutOfRangeException(nameof(storage), storage, "The durability storage is undefined."),
        };
        if (plan.DurabilityStorage is { } selected && !string.Equals(selected, name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Durability storage '{selected}' was already selected; one composition journals to one store, so '{name}' cannot also be selected.");
        }

        var first = plan.DurabilityStorage is null;
        plan.DurableExecution = true;
        plan.DurabilityStorage = name;
        var backendKey = new DurableBackendKey($"agentkit.simple.{name}");
        var journalKey = new DurableJournalKey($"agentkit.simple.{name}");
        var leaseManagerKey = new DurableLeaseManagerKey($"agentkit.simple.{name}");
        var recoveryPolicyKey = new RecoveryPolicyKey("agentkit.simple.default");
        _ = builder.Services.AddAgentDurability(configure);
        if (!first)
        {
            return builder;
        }

        // The profile below enables every first-party boundary, and a boundary whose handler is absent cannot
        // be journaled at all. Run settlement and input promotion are driven by the loop but their handlers
        // belong to AgentKit.IO, which this composition does not otherwise register, and compaction activation
        // is owned by a package that is only composed when compaction is. TryAddEnumerable makes each
        // registration idempotent with the owning package's own, because a second handler for one name is a
        // composition error.
        builder.Services.TryAddSingleton<DurableBoundaryRegistry>();
        builder.Services.TryAddEnumerable(
        [
            ServiceDescriptor.Singleton<IDurableOperationHandler, InputPromotionDurableOperationHandler>(),
            ServiceDescriptor.Singleton<IDurableOperationHandler, RunSettlementDurableOperationHandler>(),
            ServiceDescriptor.Singleton<IDurableOperationHandler, CompactionActivationDurableOperationHandler>(),
        ]);
        switch (storage)
        {
            case DurabilityStorageKind.InMemory:
                _ = builder.Services.AddInMemoryDurableExecutionBackend(backendKey, FirstPartyOperations);
                _ = builder.Services.AddInMemoryDurableOperationJournal(journalKey);
                _ = builder.Services.AddInMemoryDurableLeaseManager(leaseManagerKey);
                break;
            case DurabilityStorageKind.Sqlite:
                var sqliteTarget = new SqliteDurableStoreTarget(
                    storagePath!,
                    sqliteInstanceId ?? DefaultSqliteDurabilityInstanceId,
                    Durability.Sqlite.SqliteDatabaseOpenMode.CreateIfMissing,
                    Durability.Sqlite.SqliteSchemaMode.ApplyKnownMigrations);
                CreateParentDirectory(Path.GetDirectoryName(storagePath!)!, "SQLite durability database");
                var database = new SqliteDurableDatabase(sqliteTarget, SqliteDurableStoreSettings.CreateDefault());
                // The adapters refuse use before trusted bootstrap initialization, and this sugar is the trusted
                // composition root that named the path, so it initializes on first resolution. Registering the keyed
                // factories first makes the adapters' own TryAdd registrations no-ops for the keys while they still
                // supply the shared collaborators (observability, clock, identifier generators).
                _ = builder.Services.AddKeyedSingleton<IDurableOperationJournal>(journalKey.Value, (provider, _) =>
                {
                    var journal = new SqliteDurableOperationJournal(
                        journalKey,
                        database,
                        provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                        provider.GetRequiredService<ISecurityAuditDispatcher>(),
                        provider.GetRequiredService<ISecurityGrantStore>(),
                        provider.GetRequiredService<TimeProvider>(),
                        provider.GetRequiredService<ILogger<SqliteDurableOperationJournal>>());
                    CompleteBootstrap(journal.InitializeAsync());
                    return journal;
                });
                _ = builder.Services.AddKeyedSingleton<IDurableLeaseManager>(leaseManagerKey.Value, (provider, _) =>
                {
                    var manager = new SqliteDurableLeaseManager(
                        database,
                        provider.GetRequiredService<IIdentifierGenerator<ExecutionLeaseId>>(),
                        provider.GetRequiredService<TimeProvider>(),
                        provider.GetRequiredService<ILogger<SqliteDurableLeaseManager>>());
                    CompleteBootstrap(manager.InitializeAsync());
                    return manager;
                });
                _ = builder.Services.AddSqliteDurableExecutionBackend(backendKey, FirstPartyOperations);
                _ = builder.Services.AddSqliteDurableOperationJournal(journalKey, database);
                _ = builder.Services.AddSqliteDurableLeaseManager(leaseManagerKey, database);
                break;
            case DurabilityStorageKind.Json:
                var jsonTarget = new JsonDurableStoreTarget(
                    storagePath!,
                    jsonInstanceId ?? DefaultJsonDurabilityInstanceId,
                    JsonStoreOpenMode.CreateIfMissing,
                    JsonStoreRecoveryMode.RecoverTornAppends);
                var jsonOptions = new JsonDurableStoreOptions();
                var jsonSettings = new JsonDurableStoreSettings(
                    jsonOptions.MaximumRecordBytes,
                    jsonOptions.MaximumDocumentBytes,
                    jsonOptions.CompactionRecordThreshold,
                    jsonOptions.Encoding);
                // See the SQLite branch: initialize on first resolution as the trusted bootstrap, ahead of the
                // adapter's own no-op TryAdd for this key.
                _ = builder.Services.AddKeyedSingleton<IDurableOperationJournal>(journalKey.Value, (provider, _) =>
                {
                    var journal = new JsonDurableOperationJournal(
                        journalKey,
                        jsonTarget,
                        jsonSettings,
                        provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                        provider.GetRequiredService<ISecurityAuditDispatcher>(),
                        provider.GetRequiredService<ISecurityGrantStore>(),
                        provider.GetRequiredService<TimeProvider>(),
                        provider.GetRequiredService<ILogger<JsonDurableOperationJournal>>());
                    CompleteBootstrap(journal.InitializeAsync());
                    return journal;
                });
                _ = builder.Services.AddInMemoryDurableExecutionBackend(backendKey, FirstPartyOperations);
                _ = builder.Services.AddJsonDurableOperationJournal(journalKey, jsonTarget);
                _ = builder.Services.AddInMemoryDurableLeaseManager(leaseManagerKey);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(storage), storage, "The durability storage is undefined.");
        }

        _ = builder.Services.AddRecoveryPolicy<DefaultRecoveryPolicy>(recoveryPolicyKey);
        _ = builder.Services.AddDurabilityProfile(plan.DurabilityProfileKey, options =>
        {
            options.BackendKey = backendKey;
            options.JournalKey = journalKey;
            options.LeaseManagerKey = leaseManagerKey;
            options.RecoveryPolicyKey = recoveryPolicyKey;
            foreach (var operation in FirstPartyOperations)
            {
                options.EnabledOperations.Add(operation);
            }
        });
        return builder;
    }

    /// <summary>Observes the result of a durable-store bootstrap that completes synchronously.</summary>
    /// <remarks>
    /// Both adapters validate the root and replay their records inside <c>InitializeAsync</c> without awaiting, so the
    /// returned task is already complete; reading its result therefore never blocks, and any bootstrap failure surfaces
    /// from the resolving call instead of from a later journal operation.
    /// </remarks>
    private static void CompleteBootstrap(ValueTask initialization)
    {
        Debug.Assert(initialization.IsCompleted, "First-party durable bootstrap completes synchronously.");
        initialization.GetAwaiter().GetResult();
    }

    private static string FullyQualified(string path, string message, string parameterName)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(path), "Callers validate the path is not blank.");
        return Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : throw new ArgumentException(message, parameterName);
    }

    private static void CreateParentDirectory(string directory, string description)
    {
        try
        {
            _ = Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new InvalidOperationException($"The {description} directory '{directory}' could not be created.", exception);
        }
    }

    private enum DurabilityStorageKind
    {
        InMemory,
        Sqlite,
        Json,
    }

    extension(AgentEngineBuilder builder)
    {
        /// <summary>
        /// Opts into the defaults a local, single-user, single-process agent needs and nothing more: an in-memory
        /// session store and directory, an in-memory grant store, an allow-all security policy, best-effort audit
        /// delivery, every registered tool allowed, and a basic-assurance identity for the process user.
        /// </summary>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <remarks>
        /// Named for what it is. Nothing survives the process, every request is permitted, and the identity is the
        /// process user. A service, a multi-tenant host, or anything handling untrusted input registers its own
        /// session store, <see cref="ISecurityPolicy"/> implementations, audit sink, and authenticated identity on
        /// <see cref="AgentEngineBuilder.Services"/> instead of calling this method.
        /// </remarks>
        public AgentEngineBuilder UseLocalDevelopmentDefaults()
        {
            ArgumentNullException.ThrowIfNull(builder);
            var plan = Plan(builder);
            plan.LocalDevelopmentDefaults = true;
            _ = builder.Services.AddInMemorySecurityGrantStore();
            _ = builder.Services.AddInMemoryApprovalStore();
            _ = builder.Services.AddInMemorySecurityDecisionStore();
            _ = builder.Services.AddAllowAllSecurityPolicy();
            _ = builder.Services.AddInMemorySessionStore();
            _ = builder.Services.AddInMemorySessionDirectory(new ComponentId("agentkit.simple.session"));
            _ = builder.Services.AddAgentTools();
            _ = builder.Services.Configure<AgentPermissionOptions>(static o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
            return builder;
        }

        /// <summary>
        /// Uses one OpenAI model: registers the adapter, the API key, and a catalog descriptor whose limits,
        /// capabilities, and list prices come from the bundled <see cref="KnownModelCatalog"/>.
        /// </summary>
        /// <param name="apiKey">The OpenAI API key. Never read from the environment implicitly.</param>
        /// <param name="modelId">OpenAI's model identifier, such as <c>"gpt-4o-mini"</c>; it must exist in <see cref="KnownModelCatalog.Default"/>.</param>
        /// <param name="configure">Optional OpenAI options, such as a different base address or non-streaming transport.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="apiKey"/> or <paramref name="modelId"/> is blank, or the model is not a known OpenAI model.</exception>
        /// <remarks>
        /// Selects the model under the alias <c>assistant</c>. For a model the catalog does not know, or any other
        /// provider, register the provider's services on <see cref="AgentEngineBuilder.Services"/> and call
        /// <see cref="UseModel"/> with the alias you registered.
        /// </remarks>
        public AgentEngineBuilder UseOpenAI(string apiKey, string modelId, Action<OpenAIProviderOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

            var plan = Plan(builder);
            plan.SelectSugarModel(nameof(UseOpenAI));
            AddProviderNetwork(builder.Services);
            _ = builder.Services.AddOpenAI(configure);
            _ = builder.Services.AddOpenAIApiKeyCredential(apiKey);
            _ = builder.Services.AddOpenAIKnownLlmModel(DefaultAlias, new ModelId(modelId));
            plan.ModelAlias = DefaultAlias;
            return builder;
        }

        /// <summary>
        /// Uses one Anthropic model: registers the adapter, the API key, and a catalog descriptor whose limits,
        /// capabilities, and list prices come from the bundled <see cref="KnownModelCatalog"/>.
        /// </summary>
        /// <param name="apiKey">The Anthropic API key. Never read from the environment implicitly.</param>
        /// <param name="modelId">Anthropic's model identifier, such as <c>"claude-sonnet-4-5"</c>; it must exist in <see cref="KnownModelCatalog.Default"/>.</param>
        /// <param name="configure">Optional adapter settings such as the base address or the Anthropic API version.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="apiKey"/> or <paramref name="modelId"/> is blank, or the model is not in the catalog.</exception>
        /// <remarks>
        /// Selects the model under the alias <c>assistant</c>. Every <c>Use&lt;Provider&gt;</c> method uses that alias,
        /// so one builder calls at most one of them; a second produces a duplicate-alias composition error at build.
        /// For a model the catalog does not know, register the provider's services on
        /// <see cref="AgentEngineBuilder.Services"/> and call <see cref="UseModel"/> with the alias you registered.
        /// </remarks>
        public AgentEngineBuilder UseAnthropic(string apiKey, string modelId, Action<AnthropicProviderOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

            var plan = Plan(builder);
            plan.SelectSugarModel(nameof(UseAnthropic));
            AddProviderNetwork(builder.Services);
            _ = builder.Services.AddAnthropic(configure);
            _ = builder.Services.AddAnthropicApiKeyCredential(apiKey);
            _ = builder.Services.AddAnthropicKnownLlmModel(DefaultAlias, new ModelId(modelId));
            plan.ModelAlias = DefaultAlias;
            return builder;
        }

        /// <summary>
        /// Uses one model served by a local or remote Ollama instance through its OpenAI-compatible endpoint:
        /// registers the adapter, a credential, the model, and a catalog descriptor with the adapter's default
        /// capabilities and no limits or prices.
        /// </summary>
        /// <param name="modelId">The Ollama model tag, such as <c>"llama3.1:8b"</c>.</param>
        /// <param name="apiKey">
        /// The bearer token to send, or <see langword="null"/> to send the placeholder <c>ollama</c>, which a local
        /// server ignores. Supply a real key only for a remote server that enforces one.
        /// </param>
        /// <param name="configure">Optional adapter settings; set <see cref="OllamaProviderOptions.BaseAddress"/> for a non-default server.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="modelId"/> is blank, or <paramref name="apiKey"/> is empty or whitespace.</exception>
        /// <remarks>
        /// Ollama models are not in the known-model catalog, so the descriptor carries
        /// <see cref="OllamaProviderDefaults.DefaultCapabilities"/> and <see cref="OllamaProviderDefaults.DefaultLimits"/>.
        /// A model that cannot call tools, or whose context window you want enforced, is registered explicitly with
        /// <c>AddOllamaLlmModel</c> and <c>AddModelDescriptors</c> followed by <see cref="UseModel"/>. Selects the model
        /// under the alias <c>assistant</c>.
        /// </remarks>
        public AgentEngineBuilder UseOllama(string modelId, string? apiKey = null, Action<OllamaProviderOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
            if (apiKey is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
            }

            var plan = Plan(builder);
            plan.SelectSugarModel(nameof(UseOllama));
            AddProviderNetwork(builder.Services, allowLocalEndpoints: true);
            var descriptor = ProviderOperationDescriptorBinding.ApplyChatBinding(
                new ModelDescriptor(
                    DefaultAlias,
                    OllamaProviderDefaults.ProviderId,
                    OllamaProviderDefaults.ApiFamily,
                    new ModelId(modelId),
                    deploymentId: null,
                    OllamaProviderDefaults.DefaultCapabilities,
                    OllamaProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty),
                OllamaProviderDefaults.ChatServiceSurface,
                OllamaProviderDefaults.ChatEndpointProfileKey,
                OllamaProviderDefaults.ChatCredentialProfileKey,
                OllamaProviderDefaults.DefaultEndpointId);
            _ = builder.Services.AddOllama(configure);
            _ = builder.Services.AddOllamaApiKeyCredential(apiKey ?? OllamaPlaceholderApiKey);
            _ = builder.Services.AddOllamaLlmModel(descriptor);
            _ = builder.Services.AddModelDescriptors(DescriptorSourceId, [descriptor]);
            plan.ModelAlias = DefaultAlias;
            return builder;
        }

        /// <summary>
        /// Uses one model routed through OpenRouter: registers the adapter, the API key, the model, and a catalog
        /// descriptor with the adapter's default capabilities and no limits or prices.
        /// </summary>
        /// <param name="apiKey">The OpenRouter API key. Never read from the environment implicitly.</param>
        /// <param name="modelId">OpenRouter's namespaced model identifier, such as <c>"openai/gpt-4o-mini"</c>.</param>
        /// <param name="configure">Optional adapter settings such as the application title or referer OpenRouter attributes usage to.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="apiKey"/> or <paramref name="modelId"/> is blank.</exception>
        /// <remarks>
        /// OpenRouter routes to many vendors, so its models are not in the known-model catalog; the descriptor carries
        /// <see cref="OpenRouterProviderDefaults.DefaultCapabilities"/> and <see cref="OpenRouterProviderDefaults.DefaultLimits"/>.
        /// Register the model explicitly with <c>AddOpenRouterLlmModel</c> and <c>AddModelDescriptors</c> followed by
        /// <see cref="UseModel"/> to declare limits, prices, or narrower capabilities. Selects the model under the alias
        /// <c>assistant</c>.
        /// </remarks>
        public AgentEngineBuilder UseOpenRouter(string apiKey, string modelId, Action<OpenRouterProviderOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

            var plan = Plan(builder);
            plan.SelectSugarModel(nameof(UseOpenRouter));
            AddProviderNetwork(builder.Services);
            var descriptor = ProviderOperationDescriptorBinding.ApplyChatBinding(
                new ModelDescriptor(
                    DefaultAlias,
                    OpenRouterProviderDefaults.ProviderId,
                    OpenRouterProviderDefaults.ApiFamily,
                    new ModelId(modelId),
                    deploymentId: null,
                    OpenRouterProviderDefaults.DefaultCapabilities,
                    OpenRouterProviderDefaults.DefaultLimits,
                    pricing: null,
                    ExtensionData.Empty),
                OpenRouterProviderDefaults.ChatServiceSurface,
                OpenRouterProviderDefaults.ChatEndpointProfileKey,
                OpenRouterProviderDefaults.ChatCredentialProfileKey,
                OpenRouterProviderDefaults.DefaultEndpointId);
            _ = builder.Services.AddOpenRouter(configure);
            _ = builder.Services.AddOpenRouterApiKeyCredential(apiKey);
            _ = builder.Services.AddOpenRouterLlmModel(descriptor);
            _ = builder.Services.AddModelDescriptors(DescriptorSourceId, [descriptor]);
            plan.ModelAlias = DefaultAlias;
            return builder;
        }

        /// <summary>
        /// Uses one Azure OpenAI deployment: registers the adapter against the resource endpoint, the API key, the
        /// deployment, and a catalog descriptor whose limits, capabilities, and list prices come from the bundled
        /// <see cref="KnownModelCatalog"/> entry for the underlying OpenAI model when it has one.
        /// </summary>
        /// <param name="resourceEndpoint">The absolute <c>https</c> endpoint of the Azure OpenAI resource.</param>
        /// <param name="apiKey">The resource's API key. Never read from the environment implicitly.</param>
        /// <param name="deploymentId">The deployment name configured in the resource.</param>
        /// <param name="modelId">The OpenAI model the deployment serves, such as <c>"gpt-4o-mini"</c>; used for capabilities, limits, and prices.</param>
        /// <param name="configure">Optional adapter settings such as the API version.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="resourceEndpoint"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="apiKey"/>, <paramref name="deploymentId"/>, or <paramref name="modelId"/> is blank.</exception>
        /// <remarks>
        /// When <paramref name="modelId"/> is a known OpenAI model, its published capabilities, limits, and prices are
        /// overlaid on the Azure adapter's baseline; otherwise the adapter defaults apply. Selects the model under the
        /// alias <c>assistant</c>.
        /// </remarks>
        public AgentEngineBuilder UseAzureOpenAI(
            Uri resourceEndpoint,
            string apiKey,
            string deploymentId,
            string modelId,
            Action<AzureOpenAIProviderOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(resourceEndpoint);
            ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

            var plan = Plan(builder);
            plan.SelectSugarModel(nameof(UseAzureOpenAI));
            AddProviderNetwork(builder.Services);
            var typedModelId = new ModelId(modelId);
            var descriptor = ProviderOperationDescriptorBinding.ApplyChatBinding(
                KnownModelCatalog.Default.TryFind(OpenAIProviderDefaults.ProviderId, typedModelId, out var known)
                    ? known.ToDescriptor(DefaultAlias, AzureOpenAIProviderDefaults.ApiFamily, AzureOpenAIProviderDefaults.DefaultCapabilities) with
                    {
                        ProviderId = AzureOpenAIProviderDefaults.ProviderId,
                        DeploymentId = new DeploymentId(deploymentId),
                    }
                    : new ModelDescriptor(
                        DefaultAlias,
                        AzureOpenAIProviderDefaults.ProviderId,
                        AzureOpenAIProviderDefaults.ApiFamily,
                        typedModelId,
                        new DeploymentId(deploymentId),
                        AzureOpenAIProviderDefaults.DefaultCapabilities,
                        AzureOpenAIProviderDefaults.DefaultLimits,
                        pricing: null,
                        ExtensionData.Empty),
                AzureOpenAIProviderDefaults.ChatServiceSurface,
                AzureOpenAIProviderDefaults.ChatEndpointProfileKey,
                AzureOpenAIProviderDefaults.ChatCredentialProfileKey,
                AzureOpenAIProviderDefaults.DefaultEndpointId);
            _ = builder.Services.AddAzureOpenAI(o =>
            {
                o.ResourceEndpoint = resourceEndpoint;
                configure?.Invoke(o);
            });
            _ = builder.Services.AddAzureOpenAIApiKeyCredential(apiKey);
            _ = builder.Services.AddAzureOpenAILlmModel(descriptor);
            _ = builder.Services.AddModelDescriptors(DescriptorSourceId, [descriptor]);
            plan.ModelAlias = DefaultAlias;
            return builder;
        }

        /// <summary>
        /// Keeps conversations across restarts in one SQLite database file: registers the SQLite session store and
        /// directory and points the session profile at them. Everything else, including
        /// <see cref="UseLocalDevelopmentDefaults"/>, stays as it is.
        /// </summary>
        /// <param name="databasePath">The absolute path of the database file; its directory is created when missing.</param>
        /// <param name="instanceId">
        /// The identity stamped into the file so a different application cannot open it by accident, or
        /// <see langword="null"/> for the shared identity every engine built with this method uses.
        /// </param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="databasePath"/> is blank or not an absolute path.</exception>
        /// <exception cref="InvalidOperationException">The database file's directory does not exist and could not be created.</exception>
        /// <remarks>
        /// Sessions, and only sessions, become durable. Security grants stay in memory unless you register
        /// <c>AddSqliteSecurityGrantStore</c> yourself. Pin the agent with <see cref="WithAgentId"/> so resumed
        /// sessions keep matching the agent that created them; the default agent identity is already stable.
        /// Resume with <c>engine.Conversation.OpenAsync(sessionId)</c> and discover with <c>engine.Conversation.ListAsync</c>.
        /// </remarks>
        public AgentEngineBuilder UseSqliteSessions(string databasePath, SqliteSessionStoreInstanceId? instanceId = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            if (!Path.IsPathFullyQualified(databasePath))
            {
                throw new ArgumentException("The database path must be absolute.", nameof(databasePath));
            }

            var plan = Plan(builder);
            var fullPath = Path.GetFullPath(databasePath);
            var parentDirectory = Path.GetDirectoryName(fullPath)!;
            // AgentKit.Session.Sqlite's own database classes require this directory to already exist
            // - even under SqliteDatabaseOpenMode.CreateIfMissing, which only covers the database file
            // itself - and construct lazily through DI on first actual session use, well after this
            // registration call and any later Build() failure. Creating it here, eagerly, is therefore
            // still necessary for a fresh path to work at all; attribute a failure to this call
            // explicitly instead of letting a raw filesystem exception look unrelated to configuring
            // SQLite sessions.
            try
            {
                _ = Directory.CreateDirectory(parentDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                throw new InvalidOperationException(
                    $"The SQLite session database directory '{parentDirectory}' could not be created.", exception);
            }

            var target = new SqliteSessionStoreTarget(
                fullPath,
                instanceId ?? DefaultSqliteInstanceId,
                SqliteDatabaseOpenMode.CreateIfMissing,
                SqliteSchemaMode.ApplyKnownMigrations);
            // Stores are additive and selected by the session profile's store key, which the plan now points at
            // SQLite; the directory is singular, so any in-memory directory registered earlier is replaced.
            _ = builder.Services.AddSqliteSessionStore(target);
            _ = builder.Services.RemoveAll<ISessionDirectory>();
            _ = builder.Services.AddSqliteSessionDirectory(new ComponentId("agentkit.simple.session"), target);
            plan.DurableSessions = true;
            return builder;
        }

        /// <summary>
        /// Gives the agent a directory to work in: a sandboxed file system rooted at <paramref name="rootDirectory"/>
        /// and the read, write, edit, glob, search, and list tools over it.
        /// </summary>
        /// <param name="rootDirectory">The absolute directory the agent may see; nothing outside it is reachable.</param>
        /// <param name="configure">Optional operating-system profile configuration such as read, write, and workspace bounds; it runs after the workspace root is registered.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="rootDirectory"/> is blank or not an absolute path.</exception>
        /// <remarks>
        /// The sandbox enforces the boundary (no traversal, no symlink escape, bounded sizes) on every call, and each
        /// tool call is still authorized by the security policy first. With <see cref="UseLocalDevelopmentDefaults"/>
        /// that policy allows everything, so the agent can write anywhere under the root; register your own
        /// <see cref="ISecurityPolicy"/> to narrow that, and an approval handler to put a human in the loop.
        /// </remarks>
        public AgentEngineBuilder UseWorkspace(string rootDirectory, Action<OperatingSystemFileSystemOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
            if (!Path.IsPathFullyQualified(rootDirectory))
            {
                throw new ArgumentException("The workspace root must be absolute.", nameof(rootDirectory));
            }

            _ = Plan(builder);
            var workspaceRoot = Path.GetFullPath(rootDirectory);
            var workspaceProfile = new FileSystemProfileKey("workspace");
            var workspaceFileRoot = new FileRootId("workspace");
            _ = builder.Services.AddOperatingSystemFileSystem(workspaceProfile, o =>
            {
                o.Roots.Add(new FileRootRegistration(workspaceFileRoot, workspaceRoot));
                configure?.Invoke(o);
            });
            _ = builder.Services.AddReadTool(o =>
            {
                o.ProfileKey = workspaceProfile;
                o.RootId = workspaceFileRoot;
                o.HostRootPath = workspaceRoot;
            });
            _ = builder.Services.AddWriteTool(o =>
            {
                o.ProfileKey = workspaceProfile;
                o.RootId = workspaceFileRoot;
                o.HostRootPath = workspaceRoot;
            });
            _ = builder.Services.AddListTool(o =>
            {
                o.ProfileKey = workspaceProfile;
                o.RootId = workspaceFileRoot;
                o.HostRootPath = workspaceRoot;
            });
            _ = builder.Services.AddGlobTool(o => o.ProfileKey = workspaceProfile);
            _ = builder.Services.AddSearchTool(o => o.ProfileKey = workspaceProfile);
            _ = builder.Services.AddEditTool(o => o.ProfileKey = workspaceProfile);
            _ = builder.Services.AddPatchTool(o => o.ProfileKey = workspaceProfile);
            RegisterToolsetPublication(builder.Services, SimpleWorkspaceToolsets.Publication);
            SelectToolset(builder, SimpleWorkspaceToolsets.Key);
            return builder;
        }

        /// <summary>Registers one additive security policy consulted by the configured authority.</summary>
        /// <typeparam name="TPolicy">The policy implementation type.</typeparam>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        public AgentEngineBuilder WithPolicy<TPolicy>()
            where TPolicy : class, ISecurityPolicy
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = Plan(builder);
            _ = builder.Services.AddSecurityPolicy<TPolicy>();
            return builder;
        }

        /// <summary>
        /// Registers default network and process host boundaries for MCP stdio servers, web fetch, and command tools.
        /// </summary>
        /// <param name="processWorkspaceRoot">
        /// The absolute workspace root for process execution; defaults to the system temporary directory when omitted.
        /// </param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="processWorkspaceRoot"/> is not an absolute path.</exception>
        public AgentEngineBuilder WithHostAccess(string? processWorkspaceRoot = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = Plan(builder);
            _ = builder.Services.AddAgentNetwork();
            var root = processWorkspaceRoot ?? Path.GetTempPath();
            if (!Path.IsPathFullyQualified(root))
            {
                throw new ArgumentException("The process workspace root must be absolute.", nameof(processWorkspaceRoot));
            }

            _ = builder.Services.AddAgentProcesses(new ProcessExecutorKey("default"), options =>
            {
                options.OperatingSystem.RootDirectory = Path.GetFullPath(root);
            });
            return builder;
        }

        /// <summary>Registers one MCP endpoint as a remote tool source for the agent.</summary>
        /// <param name="sourceId">The tool source identity used during discovery.</param>
        /// <param name="endpointKey">The configured MCP endpoint key.</param>
        /// <param name="capabilityProfileId">The MCP capability profile that lists the endpoint.</param>
        /// <param name="configureClient">Optional MCP client mechanics configuration.</param>
        /// <returns>The same builder.</returns>
        public AgentEngineBuilder WithMcpServer(
            ToolSourceId sourceId,
            McpEndpointKey endpointKey,
            CapabilityProfileId capabilityProfileId,
            Action<McpClientOptions>? configureClient = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = builder.Services.AddMcpClient(configureClient);
            _ = builder.Services.AddMcpToolSource(sourceId, endpointKey, capabilityProfileId);
            _ = builder.Services.AddMcpContextContributors(new McpEndpointContextBinding(endpointKey, capabilityProfileId));
            return builder;
        }

        /// <summary>Selects one or more registered toolsets for run-bound discovery and the spec-shaped executor.</summary>
        /// <param name="toolsets">The non-empty toolset keys to resolve through the registration catalog.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="toolsets"/> is empty or contains a default key.</exception>
        public AgentEngineBuilder WithTools(params ToolsetKey[] toolsets)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(toolsets);
            if (toolsets.Length == 0)
            {
                throw new ArgumentException("At least one toolset key is required.", nameof(toolsets));
            }

            foreach (var key in toolsets)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(key, default);
                SelectToolset(builder, key);
            }

            return builder;
        }

        /// <summary>Selects the model alias the agent uses, for a provider registered directly on <see cref="AgentEngineBuilder.Services"/>.</summary>
        /// <param name="alias">The alias under which the provider package registered the model.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null, or <paramref name="alias"/> is a default value.</exception>
        public AgentEngineBuilder UseModel(ModelAlias alias)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(alias.Value, nameof(alias));
            Plan(builder).ModelAlias = alias;
            return builder;
        }

        /// <summary>Appends one system instruction. Call repeatedly to add several, in order.</summary>
        /// <param name="text">The instruction text.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
        public AgentEngineBuilder WithInstructions(string text)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            Plan(builder).Instructions.Add(text);
            return builder;
        }

        /// <summary>Uses an authenticated identity instead of the local-development default.</summary>
        /// <param name="identity">The identity every run executes under.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="identity"/> is null.</exception>
        public AgentEngineBuilder WithIdentity(ExecutionIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(identity);
            Plan(builder).Identity = identity;
            return builder;
        }

        /// <summary>Pins the agent identity, so persisted sessions and security evidence keep matching across restarts.</summary>
        /// <param name="agentId">A stable, non-default agent identity.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is the default value.</exception>
        public AgentEngineBuilder WithAgentId(AgentId agentId)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentOutOfRangeException.ThrowIfEqual(agentId.Value, Guid.Empty, nameof(agentId));
            Plan(builder).AgentId = agentId;
            return builder;
        }

        /// <summary>Bounds one run to at most <paramref name="maxTurns"/> model turns.</summary>
        /// <param name="maxTurns">A positive turn limit.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxTurns"/> is not positive.</exception>
        public AgentEngineBuilder WithMaxTurns(int maxTurns)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTurns);
            Plan(builder).MaxTurns = maxTurns;
            return builder;
        }

        /// <summary>Bounds one model attempt to <paramref name="timeout"/>.</summary>
        /// <param name="timeout">A positive duration.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
        public AgentEngineBuilder WithAttemptTimeout(TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
            Plan(builder).AttemptTimeout = timeout;
            return builder;
        }

        /// <summary>Overrides the portable request settings (temperature, reasoning effort, and so on) sent with every attempt.</summary>
        /// <param name="settings">The settings to use.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="settings"/> is null.</exception>
        public AgentEngineBuilder WithRequestSettings(LlmRequestSettings settings)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(settings);
            Plan(builder).RequestSettings = settings;
            return builder;
        }

        /// <summary>
        /// Hosts an additional agent on the same engine: its own instructions, limits, request settings, and output
        /// contract over the model, tools, identity, storage, and security the builder already selected.
        /// </summary>
        /// <param name="agentId">The additional agent's stable identity; distinct from the default agent's and from every other addition.</param>
        /// <param name="configure">Configures the agent's behavior.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default, or the configured turn limit or timeout is not positive.</exception>
        /// <exception cref="ArgumentException">The configured display name is blank.</exception>
        /// <exception cref="InvalidOperationException">The identity is already hosted by this builder.</exception>
        /// <remarks>
        /// <para>
        /// The engine publishes the additional definition next to the default one and pins a run profile for it, so
        /// <c>engine.GetAgentAsync(agentId)</c> returns a handle and <c>Agent.SendAsync</c> drives it: each turn
        /// creates or continues a session of that agent, and different sessions run concurrently. The builder's
        /// <c>Conversation</c> and <c>AskAsync</c> keep addressing the default agent.
        /// </para>
        /// <para>
        /// Every hosted agent shares the builder's model alias, security profile, and session profile. Give an
        /// agent a different model or policy by composing it on <see cref="AgentEngineBuilder.Services"/> with
        /// <c>AddAgent(AgentDefinition)</c> and its own publications.
        /// </para>
        /// </remarks>
        public AgentEngineBuilder AddAgent(AgentId agentId, Action<SimpleAgentOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new SimpleAgentOptions();
            configure(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.DisplayName, nameof(configure));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxTurns, nameof(configure));
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.AttemptTimeout, TimeSpan.Zero, nameof(configure));
            ArgumentNullException.ThrowIfNull(options.RequestSettings, nameof(configure));

            var plan = Plan(builder);
            plan.AddAgent(agentId, options);
            _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<SimpleAgentPlan>().SecurityPublication(agentId));
            _ = builder.Services.AddSingleton(provider => provider.GetRequiredService<SimpleAgentPlan>().RunProfile(agentId));
            return builder;
        }

        /// <summary>
        /// Bounds every run of every hosted agent with hard per-run limits enforced by the budget authority: turns,
        /// model requests, and tool calls are refused before the attempt, and reported tokens and cost stop the run
        /// before the next request once a limit is crossed.
        /// </summary>
        /// <param name="configure">Sets the limits; unset members impose nothing.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured limit is not positive.</exception>
        /// <exception cref="ArgumentException">No limit was configured.</exception>
        /// <remarks>
        /// The limits become the default budget profile every hosted agent selects. The in-memory ledger the first
        /// sugar call registers accounts within this process only; register <c>AddSqliteBudgetLedger</c> on
        /// <see cref="AgentEngineBuilder.Services"/> before the first sugar call for durable accounting. An exhausted limit ends
        /// the turn with a failed completion naming the dimension; <c>AskAsync</c> surfaces it as
        /// <see cref="SimpleAgentException"/>.
        /// </remarks>
        public AgentEngineBuilder WithBudget(Action<SimpleBudgetOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            var options = new SimpleBudgetOptions();
            configure(options);

            var count = new BudgetUnit("count");
            var tokens = new BudgetUnit("tokens");
            var limits = ImmutableArray.CreateBuilder<BudgetLimit>();
            Add(BudgetDimensions.Turns, options.MaxTurns, count, nameof(SimpleBudgetOptions.MaxTurns));
            Add(BudgetDimensions.ModelRequests, options.MaxModelRequests, count, nameof(SimpleBudgetOptions.MaxModelRequests));
            Add(BudgetDimensions.AttemptedToolCalls, options.MaxToolCalls, count, nameof(SimpleBudgetOptions.MaxToolCalls));
            Add(BudgetDimensions.InputTokens, options.MaxInputTokens, tokens, nameof(SimpleBudgetOptions.MaxInputTokens));
            Add(BudgetDimensions.OutputTokens, options.MaxOutputTokens, tokens, nameof(SimpleBudgetOptions.MaxOutputTokens));
            Add(BudgetDimensions.Cost, options.MaxCostUsd, new BudgetUnit("usd"), nameof(SimpleBudgetOptions.MaxCostUsd));
            if (limits.Count == 0)
            {
                throw new ArgumentException("WithBudget requires at least one limit.", nameof(configure));
            }

            Plan(builder).BudgetLimits = limits.ToImmutable();
            return builder;

            void Add(BudgetDimension dimension, decimal? value, BudgetUnit unit, string member)
            {
                if (value is not { } limit)
                {
                    return;
                }

                if (limit <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(configure), limit, $"{member} must be positive.");
                }

                limits.Add(new BudgetLimit(dimension, limit, unit, BudgetLimitKind.Hard));
            }
        }

        /// <summary>
        /// Lets agents on this engine delegate work to one another: registers the <c>task</c> tool, the goal and delegation
        /// runtime with one profile over an in-memory goal store, the local dispatcher, and the hosted worker that runs each
        /// delegated child as one turn of the target agent in its own session under the delegating identity.
        /// </summary>
        /// <param name="configure">Optional ceilings for the <c>task</c> tool's model-facing arguments.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <remarks>
        /// Every agent the tool is advertised to may delegate to any agent published on the engine (the default one
        /// and each <see cref="AddAgent"/>). To keep a specialist from delegating further, give it
        /// <see cref="SimpleAgentOptions.IncludeRegisteredTools"/> <c>false</c> or exclude <c>task</c> through
        /// toolset membership on the child definition. A child never runs inside the delegating call: the dispatcher commits
        /// a durable child goal and the worker claims and runs it, so the hosting application must start the registered hosted
        /// service. The child's turn budget is the narrower of the request and the target's own limit, and only its final
        /// answer, bounded, flows back to the parent. The in-memory goal store is ephemeral; replace the store registration
        /// for durable goals.
        /// </remarks>
        public AgentEngineBuilder WithDelegation(Action<TaskToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            var plan = Plan(builder);
            plan.Delegation = true;
            var profile = new GoalProfileReference(plan.GoalProfileKey, plan.GoalProfileVersion);
            var storeKey = new GoalStoreKey("agentkit.simple.in-memory");
            var dispatcherKey = new DelegationDispatcherKey("agentkit.simple.local");
            var worker = new GoalWorkerOptions();
            _ = builder.Services.AddAgentGoals();
            _ = builder.Services.AddInMemoryGoalStore(storeKey, options => options.AuthorizedIntentScanners.Add(worker.ScannerId));
            _ = builder.Services.AddLocalDelegationDispatcher(dispatcherKey);
            _ = builder.Services.AddGoalProfile(profile.Key, options =>
            {
                options.Version = profile.Version;
                options.StoreKey = storeKey;
                options.DispatcherKey = dispatcherKey;
            });
            _ = builder.Services.AddGoalDelegationWorker(options => options.Profiles.Add(profile));
            _ = builder.Services.AddAgentMessageChannel();
            _ = builder.Services.AddTaskTool(options =>
            {
                options.GoalProfile = profile;
                configure?.Invoke(options);
            });
            RegisterToolsetPublication(builder.Services, TaskTool.DefaultToolset);
            SelectToolset(builder, TaskTool.DefaultToolset.Key);
            return builder;
        }

        /// <summary>
        /// Gives every agent on this engine durable memory and retrieval: registers the memory runtime with one profile over an
        /// ephemeral in-memory store, keyword retrieval over active memories, and the context contributor that supplies the
        /// retrieved candidates to each model request as untrusted reference data.
        /// </summary>
        /// <param name="configure">Optional ceilings and the acceptance rule for proposed memories.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured classification is undefined.</exception>
        /// <remarks>
        /// <para>
        /// Retention stays fail-closed: a proposed memory is kept only when a registered memory policy explicitly allows it, unless
        /// <see cref="SimpleMemoryOptions.AcceptProposals"/> is set. The memory store is a protected boundary, so pair this call
        /// with <see cref="UseLocalDevelopmentDefaults"/> or register your own grant store and audit dispatcher. The in-memory
        /// store is explicitly ephemeral; register a SQLite or JSON store and a hand-written profile for durable memory, and add
        /// embeddings, documents, and vector indexes the same way.
        /// </para>
        /// </remarks>
        public AgentEngineBuilder WithMemory(Action<SimpleMemoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            var options = new SimpleMemoryOptions();
            configure?.Invoke(options);
            ArgumentOutOfRangeException.ThrowIfUndefined(options.MaximumClassification, nameof(configure));
            var plan = Plan(builder);
            plan.Memory = true;
            var storeKey = new MemoryStoreKey("agentkit.simple.in-memory");
            _ = builder.Services.AddAgentBudgets();
            if (!builder.Services.Any(static descriptor => descriptor.ServiceType == typeof(IBudgetLedger)))
            {
                _ = builder.Services.AddInMemoryBudgetLedger();
            }

            _ = builder.Services.AddAgentMemory(engine =>
            {
                if (options.AcceptProposals)
                {
                    engine.AcceptanceMode = MemoryAcceptanceMode.AllowUnlessPolicyDenies;
                }
            });
            _ = builder.Services.AddInMemoryMemoryStore(storeKey);
            _ = builder.Services.AddDurableMemoryRetrievalSource();
            _ = builder.Services.AddMemoryProfile(plan.MemoryProfileKey, profile =>
            {
                profile.EnableDurableMemory = true;
                profile.EnableRetrieval = true;
                profile.MemoryStore = storeKey;
                profile.RetrievalSources = [MemoryRetrievalSourceKeys.DurableMemory];
                profile.MaximumClassification = options.MaximumClassification;
            });
            _ = builder.Services.AddRetrievalContextContributor(
                AgentContextComponentDefaults.AssemblerKey,
                contributor => contributor.MaximumClassification = options.MaximumClassification);
            return builder;
        }

        /// <summary>
        /// Gives every agent durable binary content storage: registers the artifact coordinator with one profile, one directory,
        /// and an ephemeral in-memory store, and selects it in every hosted definition.
        /// </summary>
        /// <param name="configure">Optional ceilings for artifact size and process-output classification.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured ceiling or classification is invalid.</exception>
        /// <remarks>
        /// <para>
        /// The artifact store is a protected boundary, so pair this call with <see cref="UseLocalDevelopmentDefaults"/> or register
        /// your own grant store and audit dispatcher. The in-memory store is explicitly ephemeral: nothing survives the process.
        /// Register a SQLite, JSON, or file-system artifact store and a hand-written profile for durable content; the sugar never
        /// chooses a persistence target for you.
        /// </para>
        /// </remarks>
        public AgentEngineBuilder WithArtifacts(Action<SimpleArtifactOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            var options = new SimpleArtifactOptions();
            configure?.Invoke(options);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumArtifactBytes, nameof(configure));
            ArgumentOutOfRangeException.ThrowIfUndefined(options.ProcessOutputClassification, nameof(configure));
            var plan = Plan(builder);
            plan.Artifacts = true;
            var backend = new ArtifactBackendKey("agentkit.simple.in-memory");
            var directory = new ArtifactDirectoryId("agentkit.simple");
            _ = builder.Services.AddInMemoryArtifactStore(backend);
            _ = builder.Services.AddArtifactProfile(plan.ArtifactProfileKey, profile =>
            {
                profile.DefaultDirectory = directory;
                profile.Routes[directory] = backend;
            });
            _ = builder.Services.AddAgentArtifacts(plan.ArtifactCoordinatorKey, plan.ArtifactProfileKey, artifacts =>
            {
                artifacts.MaximumArtifactBytes = options.MaximumArtifactBytes;
                artifacts.ProcessOutputDirectory = directory;
                artifacts.ProcessOutputClassification = options.ProcessOutputClassification;
            });
            return builder;
        }

        /// <summary>
        /// Keeps long conversations inside the model's context window: when the history the loop is about to send
        /// exceeds a fraction of the selected model's declared window, older entries are summarized into a durable
        /// compaction checkpoint and the request is rebuilt from it.
        /// </summary>
        /// <param name="configure">Optional compaction settings such as the checkpoint size ceiling.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <remarks>
        /// Registers the deterministic extractive compactor under the default compactor key and one compaction profile
        /// that orders only the extractive strategy; every definition the sugar publishes selects that profile. No
        /// second model is involved. The trigger fraction is <c>AgentLoopOptions.ContextPressureThreshold</c> (0.8 by
        /// default) on the loop's named options, and the loop compacts at most once per run. Models whose descriptor
        /// declares no context window are never compacted. Calling this more than once keeps the first profile and
        /// applies each call's <paramref name="configure"/>. For model-written summaries register
        /// <c>AddModelBackedContextCompaction</c> and your own <c>AddCompactionProfile</c> on
        /// <see cref="AgentEngineBuilder.Services"/> instead of calling this method.
        /// </remarks>
        public AgentEngineBuilder WithCompaction(Action<CompactionOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            var plan = Plan(builder);
            var first = !plan.Compaction;
            plan.Compaction = true;
            _ = builder.Services.AddContextCompaction(configure);
            if (first)
            {
                _ = builder.Services.AddCompactionProfile(
                    plan.CompactionProfileKey,
                    AgentContextCompactionComponentDefaults.CompactorKey,
                    static profile => profile.StrategyOrder = [CompactionStrategyKeys.Extractive]);
            }

            return builder;
        }

        /// <summary>
        /// Records every first-party boundary as a recoverable operation: run admission, input promotion, each model
        /// request and tool call, compaction activation, deferred approval waits, and run settlement are journaled
        /// with checkpoints a recovering worker can read.
        /// </summary>
        /// <param name="configure">Optional engine-wide durability settings, such as the unknown-effect mode.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// The selected journal, lease manager, and backend are the process-local in-memory adapters, which are
        /// explicitly ephemeral: nothing here survives the process, and the lease fencing tokens are authoritative
        /// only inside it. This makes the boundaries, checkpoints, and recovery decisions real and inspectable
        /// without claiming crash recovery. For storage a restarted process can read, call
        /// <see cref="WithSqliteDurability"/> or <see cref="WithJsonDurability"/> with an explicit path; for
        /// cross-process ownership, register your own keyed <see cref="IDurableOperationJournal"/>,
        /// <see cref="IDurableLeaseManager"/>, and <see cref="IDurableExecutionBackend"/> and call
        /// <c>AddDurabilityProfile</c> on <see cref="AgentEngineBuilder.Services"/> instead. Calling this after a
        /// different durability storage was selected throws <see cref="InvalidOperationException"/>.
        /// </para>
        /// <para>
        /// The journal is a protected boundary, so it needs a grant store and an audit dispatcher: pair this with
        /// <see cref="UseLocalDevelopmentDefaults"/> or register your own. Approval waits are journaled only when
        /// the composed authority actually defers to an approval broker.
        /// </para>
        /// </remarks>
        public AgentEngineBuilder WithDurability(Action<AgentDurabilityOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return ConfigureDurability(builder, configure, DurabilityStorageKind.InMemory, storagePath: null, sqliteInstanceId: null, jsonInstanceId: null);
        }

        /// <summary>
        /// Records every first-party boundary as a recoverable operation, like <see cref="WithDurability"/>, but stores the
        /// journal, leases, and backend ownership in one SQLite database at an explicit path, so a restarted process can
        /// read the records a lost process left behind.
        /// </summary>
        /// <param name="databasePath">The absolute path of the SQLite database file; it is created when absent.</param>
        /// <param name="configure">Optional engine-wide durability settings, such as the unknown-effect mode.</param>
        /// <param name="instanceId">The expected store instance identity, or null for the sugar's default.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="databasePath"/> is blank or not absolute.</exception>
        /// <exception cref="InvalidOperationException">
        /// A different durability storage was already selected, or the database directory could not be created.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The path is never implied: durable storage is an external fact the caller names. The database is validated,
        /// created, and bound to <paramref name="instanceId"/> when the engine first resolves the journal or lease
        /// manager, so a path or schema problem surfaces from that resolution. SQLite is durable local
        /// storage; it provides no distributed lease, fencing across machines, or cross-store atomicity, so ownership is
        /// authoritative on one host sharing that file. The journal is still a protected boundary, so pair this with
        /// <see cref="UseLocalDevelopmentDefaults"/> or register your own grant store and audit dispatcher.
        /// </para>
        /// </remarks>
        public AgentEngineBuilder WithSqliteDurability(
            string databasePath,
            Action<AgentDurabilityOptions>? configure = null,
            SqliteDurableStoreInstanceId? instanceId = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            return ConfigureDurability(
                builder,
                configure,
                DurabilityStorageKind.Sqlite,
                FullyQualified(databasePath, "The database path must be absolute.", nameof(databasePath)),
                instanceId,
                jsonInstanceId: null);
        }

        /// <summary>
        /// Records every first-party boundary as a recoverable operation, like <see cref="WithDurability"/>, but stores the
        /// journal as inspectable newline-delimited JSON files under an explicit directory.
        /// </summary>
        /// <param name="directoryPath">The absolute directory that holds the journal files; it is created when absent.</param>
        /// <param name="configure">Optional engine-wide durability settings, such as the unknown-effect mode.</param>
        /// <param name="instanceId">The expected store instance identity, or null for the sugar's default.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="directoryPath"/> is blank or not absolute.</exception>
        /// <exception cref="InvalidOperationException">A different durability storage was already selected.</exception>
        /// <remarks>
        /// <para>
        /// The path is never implied, and it must be canonical: the store refuses a root that traverses a symbolic link
        /// (macOS reaches its temp directory through one). The journal is locked, validated, and replayed when the engine
        /// first resolves it. The JSON journal flushes every acknowledged record, recovers a torn trailing append,
        /// and holds an advisory exclusive lock, so a second writer on the directory is refused. The JSON family provides
        /// no lease manager or backend, so those remain the process-local in-memory adapters: fencing tokens are
        /// authoritative only inside the process, and the sugar claims no multi-process coordination. The journal is still
        /// a protected boundary, so pair this with <see cref="UseLocalDevelopmentDefaults"/> or register your own grant
        /// store and audit dispatcher.
        /// </para>
        /// </remarks>
        public AgentEngineBuilder WithJsonDurability(
            string directoryPath,
            Action<AgentDurabilityOptions>? configure = null,
            JsonDurableStoreInstanceId? instanceId = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
            return ConfigureDurability(
                builder,
                configure,
                DurabilityStorageKind.Json,
                FullyQualified(directoryPath, "The journal directory path must be absolute.", nameof(directoryPath)),
                sqliteInstanceId: null,
                instanceId);
        }

        /// <summary>
        /// Requires every final answer to satisfy a structured-output contract: the loop validates each terminal
        /// response through the composed output processor, asks the model to correct a rejected candidate within
        /// the definition's retry policy, and surfaces the accepted value on the turn result.
        /// </summary>
        /// <param name="definition">The complete, immutable output definition.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="definition"/> is null.</exception>
        /// <remarks>
        /// In <see cref="OutputMode.Prompted"/> the schema reaches the model only through instructions, so pair this
        /// call with <see cref="WithInstructions"/> describing the expected JSON, or use <see cref="WithOutput{T}"/>,
        /// which adds that instruction for you. The output processor registered by the first sugar call is the
        /// first-party one; replace it on <see cref="AgentEngineBuilder.Services"/> when you need another.
        /// </remarks>
        public AgentEngineBuilder WithOutput(OutputDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(definition);
            Plan(builder).Output = definition;
            return builder;
        }

        /// <summary>
        /// Requires every final answer to be JSON matching <paramref name="schemaJson"/>, deserialized to
        /// <typeparamref name="T"/>, and tells the model so through a definition-level instruction.
        /// </summary>
        /// <typeparam name="T">The application type the validated JSON is deserialized into; it must be constructible by <c>System.Text.Json</c>.</typeparam>
        /// <param name="schemaJson">A JSON Schema (draft 2020-12 structural subset) the answer must validate against.</param>
        /// <param name="name">A short name for the contract, used in diagnostics; defaults to the type's name.</param>
        /// <param name="maximumRepairAttempts">
        /// How many times the model may be asked to correct a rejected candidate before the turn fails; the composed
        /// processor's own ceiling also applies. Defaults to 2.
        /// </param>
        /// <param name="mode"></param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="schemaJson"/> is blank or not a JSON object, or <paramref name="name"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumRepairAttempts"/> is negative.</exception>
        /// <exception cref="System.Text.Json.JsonException"><paramref name="schemaJson"/> is not valid JSON.</exception>
        /// <remarks>
        /// Uses <see cref="OutputMode.Prompted"/>: the model is instructed to answer with only the JSON object and
        /// the processor validates the text it returns. Read the accepted value with <c>AskAsync&lt;T&gt;</c> or from
        /// <see cref="ConversationTurnResult.Output"/>.
        /// </remarks>
        public AgentEngineBuilder WithOutput<T>(
            string schemaJson,
            string? name = null,
            int maximumRepairAttempts = 2,
            OutputMode mode = OutputMode.Prompted)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
            if (name is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);
            }

            ArgumentOutOfRangeException.ThrowIfNegative(maximumRepairAttempts);
            ArgumentOutOfRangeException.ThrowIfUndefined(mode);

            using var document = System.Text.Json.JsonDocument.Parse(schemaJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                throw new ArgumentException("The schema must be a JSON object.", nameof(schemaJson));
            }

            var contractName = name ?? typeof(T).Name;
            var schema = document.RootElement.Clone();
            var definition = new OutputDefinition(
                new OutputDefinitionId($"agentkit.simple.output/{contractName}"),
                new OutputDefinitionVersion("1"),
                contractName,
                mode,
                new JsonSchemaDocument(contractName, new SchemaVersion("1"), schema),
                typeof(T),
                alternatives: [],
                validators: [],
                OutputValidationPolicy.RejectOnFirstFailure,
                new OutputRetryPolicy(maximumRepairAttempts),
                OutputEndStrategy.Graceful);

            var plan = Plan(builder);
            plan.Output = definition;
            if (mode is OutputMode.Prompted)
            {
                plan.Instructions.Add(
                    $"Your final answer must be a single JSON object that validates against this JSON Schema, with no " +
                    $"prose, code fences, or commentary before or after it:\n{schema.GetRawText()}");
            }

            return builder;
        }
    }

    private static void SelectToolset(AgentEngineBuilder builder, ToolsetKey key)
    {
        var plan = Plan(builder);
        if (!plan.ToolsetKeys.Contains(key))
        {
            plan.ToolsetKeys.Add(key);
        }

        SimpleToolRuntime.EnsureRegistered(builder.Services);
    }

    private static void RegisterToolsetPublication(IServiceCollection services, ToolsetPublication publication)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(publication);
        var publicationKey = publication.Key;
        if (services.Any(descriptor =>
                descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(ToolsetPublication)
                && descriptor.ServiceKey is ToolsetKey key
                && key == publicationKey))
        {
            return;
        }

        _ = services.AddToolset(publication);
    }

    /// <summary>
    /// Registers the network boundary every first-party provider adapter sends through, under the default
    /// internet-only destination policy unless a local endpoint is requested.
    /// </summary>
    /// <param name="services">The builder's service collection.</param>
    /// <param name="allowLocalEndpoints">
    /// <see langword="true"/> for a model server on the local machine or a private network (such as Ollama), which
    /// the default policy would refuse; the policy then also allows <c>http</c> and private addresses.
    /// </param>
    /// <remarks>
    /// Provider egress is authorized and enforced like every other network effect, so a provider-backed builder always
    /// composes <see cref="AgentNetworkOptions"/>; <c>TryAdd</c> registration keeps any network an application
    /// registered first. The local-endpoint policy is a single-user convenience: a service that fetches web content
    /// as well should register its own <c>AddAgentNetwork</c> with a policy naming exactly the hosts it allows.
    /// </remarks>
    private static void AddProviderNetwork(IServiceCollection services, bool allowLocalEndpoints = false)
    {
        Debug.Assert(services is not null, "Public extension methods validate the builder first.");
        _ = services.AddAgentNetwork(options =>
        {
            if (allowLocalEndpoints)
            {
                options.DestinationPolicy = new NetworkDestinationPolicy(["http", "https"], allowedHosts: null, allowPrivateAddresses: true);
            }
        });
    }

    /// <summary>
    /// Returns the builder's plan, registering it and every plan-driven service the first time. Each of those
    /// registrations reads the plan lazily through DI, so later sugar calls still take effect.
    /// </summary>
    /// <param name="builder">The builder whose service collection carries the plan.</param>
    /// <returns>The single plan instance for <paramref name="builder"/>.</returns>
    internal static SimpleAgentPlan Plan(AgentEngineBuilder builder)
    {
        Debug.Assert(builder is not null, "Public extension methods validate the builder first.");
        var services = builder.Services;
        if (services.FirstOrDefault(static d => d.ServiceType == typeof(SimpleAgentPlan))?.ImplementationInstance is SimpleAgentPlan existing)
        {
            return existing;
        }

        var plan = new SimpleAgentPlan();
        _ = services.AddSingleton(plan);

        // Runtime spine. Each registration is TryAdd-based, so anything the application registered first wins.
        _ = services.AddAgentProviders();
        _ = services.AddAgentSession();
        _ = services.AddAgentContext();
        _ = services.AddAgentOutput();
        _ = services.AddAgentLoop(AgentLoopComponentDefaults.LoopKey);
        _ = services.AddAgentIO(AgentIOComponentDefaults.InputCoordinatorKey, AgentIOComponentDefaults.OutputPublisherKey);
        _ = services.AddSessionBackedInputQueue();
        _ = services.AddAgentHooks();
        _ = services.AddAgentTools();

        // Budgets: every definition selects a run budget profile, so the authority, one ledger, and the default
        // profile are part of the spine. The profile reads the plan's limits lazily, so WithBudget in any order
        // still takes effect; with no WithBudget the profile has no limits and bounds nothing. The in-memory ledger
        // is added only when none is registered yet: register a durable ledger before the first sugar call (or use
        // ReplaceBudgetLedger) to account durably.
        _ = services.AddAgentBudgets();
        _ = services.AddBudgetProfile(AgentBudgetComponentDefaults.ProfileKey, profile =>
        {
            foreach (var limit in plan.BudgetLimits)
            {
                profile.Limits.Add(limit);
            }
        });
        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(IBudgetLedger)))
        {
            _ = services.AddInMemoryBudgetLedger();
        }

        // Security: the standalone-profile pieces, with the policy snapshot and publication read from the plan so
        // the engine's pinned run profile and the authority's options agree by construction.
        _ = services.AddAgentPermissions(o =>
        {
            o.PolicyVersion = plan.PolicySnapshot.Version.Value;
            o.PolicySnapshot = plan.PolicySnapshot;
        });
        _ = services.AddSecurityAuthority(plan.AuthorityKey);
        _ = services.AddSingleton(static provider => provider.GetRequiredService<SimpleAgentPlan>().SecurityPublication());
        _ = services.AddSingleton(static provider => provider.GetRequiredService<SimpleAgentPlan>().RunProfile());

        // The engine catalog: one definition, its bootstrap snapshot materialized without I/O.
        _ = services.AddSingleton<SimpleAgentDefinitionSource>();
        _ = services.AddSingleton<IAgentDefinitionSource>(static provider => provider.GetRequiredService<SimpleAgentDefinitionSource>());
        _ = services.AddSingleton(static provider => provider.GetRequiredService<SimpleAgentDefinitionSource>().Snapshot);

        // One conversation over the same plan, advertising every registered tool with its captured descriptor.
        _ = services.AddConversationSession(static _ => { });
        _ = services.AddOptions<ConversationSessionOptions>()
            .Configure<SimpleAgentPlan, IEnumerable<RegisteredToolInvoker>>(static (options, current, registrations) =>
            {
                current.Apply(options);
                var descriptors = SimpleAgentPlan.AdvertisedTools(registrations);
                var definitions = descriptors.ToLlmToolDefinitions();
                for (var index = 0; index < descriptors.Length; index++)
                {
                    options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(descriptors[index], definitions[index]));
                }
            });

        return plan;
    }
}
