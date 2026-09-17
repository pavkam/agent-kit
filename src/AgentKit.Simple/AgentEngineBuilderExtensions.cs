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
    /// <summary>The alias <see cref="UseOpenAI"/> registers its model under.</summary>
    internal static ModelAlias DefaultAlias { get; } = new("assistant");

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
            _ = builder.Services.AddOpenAI(configure);
            _ = builder.Services.AddOpenAIApiKeyCredential(apiKey);
            _ = builder.Services.AddOpenAIKnownLlmModel(DefaultAlias, new ModelId(modelId));
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
        public AgentEngineBuilder UseWorkspace(string rootDirectory, Action<SandboxedFileSystemOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
            if (!Path.IsPathFullyQualified(rootDirectory))
            {
                throw new ArgumentException("The workspace root must be absolute.", nameof(rootDirectory));
            }

            _ = Plan(builder);
            _ = builder.Services.AddSandboxedFileSystem(Path.GetFullPath(rootDirectory), configure);
            _ = builder.Services.AddReadTool();
            _ = builder.Services.AddWriteTool();
            _ = builder.Services.AddEditTool();
            _ = builder.Services.AddGlobTool();
            _ = builder.Services.AddSearchTool();
            _ = builder.Services.AddListTool();
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
        _ = services.AddAgentTools();

        // Security: the standalone-profile pieces, with the policy snapshot and publication read from the plan so
        // the engine's pinned run profile and the authority's options agree by construction.
        _ = services.AddAgentPermissions(o =>
        {
            o.PolicyVersion = plan.PolicySnapshot.Version.Value;
            o.PolicySnapshot = plan.PolicySnapshot;
        });
        _ = services.AddSecurityAuthority(plan.AuthorityKey);
        _ = services.AddSingleton(static provider => provider.GetRequiredService<AgentRunProfilePublication>().SecurityProfile);
        _ = services.AddSingleton(static provider => provider.GetRequiredService<SimpleAgentPlan>().RunProfile());

        // The engine catalog: one definition, its bootstrap snapshot materialized without I/O.
        _ = services.AddSingleton<SimpleAgentDefinitionSource>();
        _ = services.AddSingleton<IAgentDefinitionSource>(static provider => provider.GetRequiredService<SimpleAgentDefinitionSource>());
        _ = services.AddSingleton(static provider => provider.GetRequiredService<SimpleAgentDefinitionSource>().Snapshot);

        // One conversation over the same plan, advertising every registered tool with its captured descriptor.
        _ = services.AddConversationSession(static _ => { });
        _ = services.AddOptions<ConversationSessionOptions>()
            .Configure<SimpleAgentPlan, IEnumerable<ITool>>(static (options, current, tools) =>
            {
                current.Apply(options);
                var descriptors = tools.Select(static tool => tool.Descriptor).ToImmutableArray();
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
