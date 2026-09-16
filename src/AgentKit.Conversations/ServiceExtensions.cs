// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Registers the first-party ready-to-use conversation session.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultConversationSession"/> as <see cref="IConversationSession"/>, together with
        /// the turn-scoped identifier generators it needs.
        /// </summary>
        /// <param name="configure">Configures the one composed agent this session drives turns for.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <remarks>
        /// This does not register <c>ISessionCoordinator</c>, <c>ISecurityProfileSelector</c>, <c>IAgentLoop</c>,
        /// <c>IContextAssembler</c>, <c>IToolInvoker</c>, <c>IModelCatalog</c>, <c>IModelSelector</c>,
        /// <c>ILlmModelResolver</c>, or the keyed <c>IRunContinuationPolicy</c>: the host still selects a session
        /// store, security policies and authority, and the tool/provider registrations its one agent needs, exactly
        /// as any other AgentKit composition does. This type drives exactly one agent and never selects among
        /// several keyed loops, so every one of those collaborators is resolved unkeyed except the loop and the
        /// continuation policy. <c>IAgentLoop</c> is registered scoped by its own package (for example
        /// <c>AgentKit.Loop</c>'s <c>AddAgentLoop</c>), so <see cref="DefaultConversationSession"/> resolves it
        /// from a short-lived scope created per turn under the fixed key named by
        /// <see cref="AgentLoopComponentDefaults.LoopKey"/> rather than capturing it at construction, which would
        /// otherwise make this singleton-lifetime session a captive dependency on a shorter-lived service. The
        /// continuation policy is resolved from the fixed key named by
        /// <see cref="AgentLoopComponentDefaults.ContinuationPolicyKey"/>. Every registration here is idempotent
        /// (<c>TryAdd</c>) except the bound options, so calling this more than once with different
        /// <paramref name="configure"/> delegates applies every delegate to the same options instance in call order.
        /// </remarks>
        public IServiceCollection AddConversationSession(Action<ConversationSessionOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            _ = services.AddOptions<ConversationSessionOptions>()
                .Configure(configure)
                .Validate(static options => options.AgentId != default, "AgentId must be set.")
                .Validate(static options => options.Identity is not null, "Identity must be set.")
                .Validate(static options => options.SecurityProfileKey != default, "SecurityProfileKey must be set.")
                .Validate(static options => options.ConfigurationVersion != default, "ConfigurationVersion must be set.")
                .Validate(static options => options.SessionProfile is not null, "SessionProfile must be set.")
                .Validate(static options => options.ModelSelectionPolicy is not null, "ModelSelectionPolicy must be set.")
                .Validate(static options => options.MaxTurns > 0, "MaxTurns must be positive.")
                .Validate(static options => options.AttemptTimeout > TimeSpan.Zero, "AttemptTimeout must be positive.")
                .ValidateOnStart();

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<RunId>>(
                static _ => new GuidIdentifierGenerator<RunId>(static guid => new RunId(guid)));
            services.TryAddSingleton<IIdentifierGenerator<OperationId>>(
                static _ => new GuidIdentifierGenerator<OperationId>(static guid => new OperationId(guid)));
            services.TryAddSingleton<IIdentifierGenerator<MessageId>>(
                static _ => new GuidIdentifierGenerator<MessageId>(static guid => new MessageId(guid)));
            services.TryAddSingleton<IIdentifierGenerator<SessionEntryId>>(
                static _ => new GuidIdentifierGenerator<SessionEntryId>(static guid => new SessionEntryId(guid)));
            services.TryAddSingleton<IConversationSession, DefaultConversationSession>();
            return services;
        }
    }
}
