// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

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
            _ = builder.Services.AddAgentTools(static o => o.AllowAllRegisteredTools = true);
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
            var descriptor = new ModelDescriptor(
                DefaultAlias,
                OllamaProviderDefaults.ProviderId,
                OllamaProviderDefaults.ApiFamily,
                new ModelId(modelId),
                deploymentId: null,
                OllamaProviderDefaults.DefaultCapabilities,
                OllamaProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty);
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
            var descriptor = new ModelDescriptor(
                DefaultAlias,
                OpenRouterProviderDefaults.ProviderId,
                OpenRouterProviderDefaults.ApiFamily,
                new ModelId(modelId),
                deploymentId: null,
                OpenRouterProviderDefaults.DefaultCapabilities,
                OpenRouterProviderDefaults.DefaultLimits,
                pricing: null,
                ExtensionData.Empty);
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
            var typedModelId = new ModelId(modelId);
            var descriptor = KnownModelCatalog.Default.TryFind(OpenAIProviderDefaults.ProviderId, typedModelId, out var known)
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
                    ExtensionData.Empty);
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
        /// <param name="configure">Optional file-system bounds such as maximum read and write sizes.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="rootDirectory"/> is blank or not an absolute path.</exception>
        /// <remarks>
        /// The sandbox enforces the boundary (no traversal, no symlink escape, bounded sizes) on every call, and each
        /// tool call is still authorized by the security policy first. With <see cref="UseLocalDevelopmentDefaults"/>
        /// that policy allows everything, so the agent can write anywhere under the root; register your own
        /// <see cref="ISecurityPolicy"/> to narrow that, and an approval handler to put a human in the loop.
        /// </remarks>
        [Obsolete("Prefer keyed AddOperatingSystemFileSystem and tool options HostRootPath.")]
        public AgentEngineBuilder UseWorkspace(string rootDirectory, Action<SandboxedFileSystemOptions>? configure = null)
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
            _ = builder.Services.AddSandboxedFileSystem(workspaceRoot, configure);
            _ = builder.Services.AddOperatingSystemFileSystem(workspaceProfile, o =>
                o.Roots.Add(new FileRootRegistration(workspaceFileRoot, workspaceRoot)));
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
        /// Bounds every run of the default agent with hard per-run limits enforced by the budget authority: turns,
        /// model requests, and tool calls are refused before the attempt, and reported tokens and cost stop the run
        /// before the next request once a limit is crossed.
        /// </summary>
        /// <param name="configure">Sets the limits; unset members impose nothing.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured limit is not positive.</exception>
        /// <exception cref="ArgumentException">No limit was configured.</exception>
        /// <remarks>
        /// Registers the budget authority and, unless a ledger is already registered, the in-memory ledger, which
        /// accounts within this process only; register <c>AddSqliteBudgetLedger</c> on
        /// <see cref="AgentEngineBuilder.Services"/> before this call for durable accounting. An exhausted limit ends
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

            var plan = Plan(builder);
            plan.BudgetLimits = limits.ToImmutable();
            _ = builder.Services.AddAgentBudgets();
            if (!builder.Services.Any(static descriptor => descriptor.ServiceType == typeof(IBudgetLedger)))
            {
                _ = builder.Services.AddInMemoryBudgetLedger();
            }

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
        /// Lets agents on this engine delegate work to one another: registers the <c>task</c> tool, the delegation
        /// broker, and the engine-backed channel that runs a delegated task as one turn of the target agent in a new
        /// session under the delegating identity.
        /// </summary>
        /// <param name="configure">Optional ceilings for the <c>task</c> tool's model-facing arguments.</param>
        /// <returns>The same builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
        /// <remarks>
        /// Every agent the tool is advertised to may delegate to any agent published on the engine (the default one
        /// and each <see cref="AddAgent"/>). To keep a specialist from delegating further, give it
        /// <see cref="SimpleAgentOptions.IncludeRegisteredTools"/> <c>false</c> or exclude <c>task</c> through
        /// <c>AgentToolsOptions.AllowedToolIds</c>. The child's turn budget is the narrower of the request and the
        /// target's own limit, and only its final answer, bounded, flows back to the parent.
        /// </remarks>
        public AgentEngineBuilder WithDelegation(Action<TaskToolOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = Plan(builder);
            _ = builder.Services.AddEngineDelegationChannel();
            _ = builder.Services.AddAgentDelegation();
            _ = builder.Services.AddTaskTool(configure);
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
        /// Registers the deterministic extractive compactor; no second model is involved. The trigger fraction is
        /// <c>AgentLoopOptions.ContextPressureThreshold</c> (0.8 by default) on the loop's named options, and the loop
        /// compacts at most once per run. Models whose descriptor declares no context window are never compacted.
        /// For model-written summaries register <c>AddModelBackedContextCompaction</c> on
        /// <see cref="AgentEngineBuilder.Services"/> instead of calling this method.
        /// </remarks>
        public AgentEngineBuilder WithCompaction(Action<CompactionOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            _ = Plan(builder);
            _ = builder.Services.AddContextCompaction(configure);
            return builder;
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
        public AgentEngineBuilder WithOutput<T>(string schemaJson, string? name = null, int maximumRepairAttempts = 2)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaJson);
            if (name is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(name);
            }

            ArgumentOutOfRangeException.ThrowIfNegative(maximumRepairAttempts);

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
                OutputMode.Prompted,
                new JsonSchemaDocument(contractName, new SchemaVersion("1"), schema),
                typeof(T),
                alternatives: [],
                validators: [],
                OutputValidationPolicy.RejectOnFirstFailure,
                new OutputRetryPolicy(maximumRepairAttempts),
                OutputEndStrategy.Graceful);

            var plan = Plan(builder);
            plan.Output = definition;
            plan.Instructions.Add(
                $"Your final answer must be a single JSON object that validates against this JSON Schema, with no " +
                $"prose, code fences, or commentary before or after it:\n{schema.GetRawText()}");
            return builder;
        }
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
        _ = services.AddAgentHooks();
        _ = services.AddAgentTools();

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
            .Configure<SimpleAgentPlan, IEnumerable<ITool>, IOptions<AgentToolsOptions>>(static (options, current, tools, toolOptions) =>
            {
                current.Apply(options);
                var descriptors = SimpleAgentPlan.AdvertisedTools(tools, toolOptions.Value);
                var definitions = descriptors.ToLlmToolDefinitions();
                for (var index = 0; index < descriptors.Length; index++)
                {
                    options.Tools.Add(definitions[index]);
                    options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(descriptors[index], definitions[index]));
                }
            });

        return plan;
    }
}
