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
        /// This does not register <c>ISessionCoordinator</c>, <c>ISecurityProfileSelector</c>, or
        /// <c>AgentEngine</c>: the host still selects a session store, security policies and authority, the
        /// loop/provider/tool registrations its one agent needs, and <c>AddAgentKit</c>, exactly as any other
        /// AgentKit composition does. Turns delegate through <see cref="EngineConversationTurnExecutor"/> to
        /// typed <see cref="Agent"/> run methods rather than compiling a private <c>AgentRunServices</c> bundle.
        /// Every registration here is idempotent
        /// (<c>TryAdd</c>) except the bound options, so calling this more than once with different
        /// <paramref name="configure"/> delegates applies every delegate to the same options instance in call order.
        /// </remarks>
        public IServiceCollection AddConversationSession(Action<ConversationSessionOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            _ = services.AddOptions<ConversationSessionOptions>()
                .Configure(configure)
                .Validate(static options => options.Agent is not null, "Agent must be set.")
                .Validate(static options => options.Configuration is not null, "Configuration must be set.")
                .Validate(static options => options.Identity is not null, "Identity must be set.")
                .Validate(static options => options.SessionProfile is not null, "SessionProfile must be set.")
                .ValidateOnStart();

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<OperationId>>(
                static _ => new GuidIdentifierGenerator<OperationId>(static guid => new OperationId(guid)));
            services.TryAddSingleton<IConversationTurnExecutor, EngineConversationTurnExecutor>();
            services.TryAddSingleton<IConversationEngineHost, ConversationEngineHost>();
            services.TryAddSingleton<IConversationSession, DefaultConversationSession>();
            return services;
        }
    }
}
