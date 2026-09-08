// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Registers the first-party AgentKit permission and grant-lifecycle services.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds the concurrency-safe process-local security grant store without replacing a host-supplied implementation.</summary>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddAgentPermissions(Action<AgentPermissionOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            var options = services.AddOptions<AgentPermissionOptions>()
                .Validate(static value => value.PolicyVersion > 0, "PolicyVersion must be positive.")
                .Validate(static value => value.RevocationVersion > 0, "RevocationVersion must be positive.")
                .Validate(static value => value.MaximumGrantLifetime > TimeSpan.Zero, "MaximumGrantLifetime must be positive.")
                .Validate(static value => value.MaximumGrantUses > 0, "MaximumGrantUses must be positive.")
                .Validate(static value => Enum.IsDefined(value.AuditDelivery), "AuditDelivery must be defined.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<ISecurityGrantStore, InMemorySecurityGrantStore>();
            services.TryAddSingleton<IIdentifierGenerator<GrantId>, GuidGrantIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>();
            services.TryAddSingleton<ISecurityAuthority, SecurityAuthority>();
            services.TryAddSingleton<ISecurityAuthoritySelector, DefaultSecurityAuthoritySelector>();
            services.TryAddSingleton<ISecurityAuditDispatcher, DefaultSecurityAuditDispatcher>();
            services.TryAddSingleton<ISecurityProfilePublicationReader, DefaultSecurityProfilePublicationReader>();
            services.TryAddSingleton<ISecurityProfileSelector, DefaultSecurityProfileSelector>();
            return services;
        }

        /// <summary>Registers one immutable security-profile publication for its exact composition coordinates.</summary>
        /// <param name="publication">The non-null publication selected by the host composition.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="publication"/> is null.</exception>
        /// <remarks>
        /// Registrations are additive and the host retains ownership of the supplied immutable publication instance.
        /// The default reader freezes the registered references and rejects duplicate exact agent, definition-revision,
        /// configuration-version, and profile-key coordinates when it is activated; callers must register each complete
        /// coordinate at most once.
        /// </remarks>
        public IServiceCollection AddSecurityProfilePublication(SecurityProfilePublication publication)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(publication);
            _ = services.AddSingleton(publication);
            return services;
        }


        /// <summary>Registers one host-owned audit sink with explicit supported-event and durability semantics.</summary>
        /// <param name="registration">The immutable event support, delivery, and durable-acceptance declaration.</param>
        /// <param name="sink">The host-owned sink instance.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="registration"/>, or <paramref name="sink"/> is null.</exception>
        /// <remarks>The container does not own disposal of the supplied instance. Required delivery is rejected by the dispatcher when no compatible durable sink accepts the record; this method supplies no no-op fallback.</remarks>
        public IServiceCollection AddSecurityAuditSink(
            SecurityAuditSinkRegistration registration,
            ISecurityAuditSink sink)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(registration);
            ArgumentNullException.ThrowIfNull(sink);
            _ = services.AddSingleton(new SecurityAuditSinkBinding(registration, sink));
            return services;
        }

        /// <summary>
        /// Registers one host-owned singleton authority under the exact key
        /// that an authorization context must capture to select it.
        /// </summary>
        /// <param name="authorityKey">The non-default key that identifies the authority binding.</param>
        /// <param name="authority">The host-owned singleton authority instance.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="authority"/> is null.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="authorityKey"/> is default.</exception>
        /// <exception cref="ArgumentException"><paramref name="authorityKey"/> is blank.</exception>
        /// <remarks>
        /// The container does not own disposal of the supplied instance. Reusing a key is a composition
        /// error reported when the singular selector is constructed; it never selects whichever binding
        /// happened to be registered first. This method deliberately does not register an unkeyed fallback.
        /// </remarks>
        public IServiceCollection AddSecurityAuthority(
            ComponentKey<ISecurityAuthority> authorityKey,
            ISecurityAuthority authority)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(authorityKey.Value, nameof(authorityKey));
            ArgumentNullException.ThrowIfNull(authority);
            _ = services.AddSingleton(new SecurityAuthorityBinding(authorityKey, authority));
            return services;
        }
    }
}
