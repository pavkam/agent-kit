// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Registers the first-party AgentKit security runtime without selecting persistent grant storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds the first-party security runtime without selecting a security-grant storage backend.</summary>
        /// <param name="configure">An optional configuration delegate applied to the validated runtime options.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// This registration supplies policy, authority, profile, audit, identifier, and clock services. It deliberately
        /// does not register <see cref="ISecurityGrantStore"/>. The host must explicitly select exactly one store adapter
        /// before resolving an authority that can issue grants; missing storage fails composition before protected work.
        /// </remarks>
        public IServiceCollection AddAgentPermissions(Action<AgentPermissionOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            var options = services.AddOptions<AgentPermissionOptions>()
                .Validate(static value => value.PolicyVersion > 0, "PolicyVersion must be positive.")
                .Validate(static value => value.PolicySnapshot is null || value.PolicySnapshot.Version.Value == value.PolicyVersion,
                    "PolicySnapshot.Version must equal PolicyVersion.")
                .Validate(static value => value.RevocationVersion > 0, "RevocationVersion must be positive.")
                .Validate(static value => value.MaximumGrantLifetime > TimeSpan.Zero, "MaximumGrantLifetime must be positive.")
                .Validate(static value => value.MaximumGrantUses > 0, "MaximumGrantUses must be positive.")
                .Validate(static value => Enum.IsDefined(value.AuditDelivery), "AuditDelivery must be defined.")
                .Validate(static value => AgentPermissionOptions.IsSupportedAuditDeliveryTimeout(value.AuditDeliveryTimeout),
                    "AuditDeliveryTimeout must be positive and within the supported timer range.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<GrantId>, GuidGrantIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<ApprovalRequestId>, GuidApprovalRequestIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();
            services.TryAddSingleton<IApprovalHandler, DenyApprovalHandler>();
            services.TryAddSingleton<IApprovalResponderAuthorizer, DenyApprovalResponderAuthorizer>();
            services.TryAddSingleton<IApprovalBroker>(static provider => new DefaultApprovalBroker(
                provider.GetRequiredService<IApprovalStore>(),
                provider.GetRequiredService<IApprovalHandler>(),
                provider.GetRequiredService<IApprovalResponderAuthorizer>(),
                provider.GetRequiredService<ISecurityAuditDispatcher>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                provider.GetRequiredService<TimeProvider>()));
            services.TryAddSingleton<ISecurityAuthority>(static provider =>
            {
                var policies = provider.GetServices<ISecurityPolicy>();
                var grantStore = provider.GetRequiredService<ISecurityGrantStore>();
                var grantIds = provider.GetRequiredService<IIdentifierGenerator<GrantId>>();
                var timeProvider = provider.GetRequiredService<TimeProvider>();
                var permissionOptions = provider.GetRequiredService<IOptions<AgentPermissionOptions>>();
                var logger = provider.GetService<ILogger<SecurityAuthority>>();
                return provider.GetService<IApprovalStore>() is null
                    ? new SecurityAuthority(policies, grantStore, grantIds, timeProvider, permissionOptions, logger)
                    : new SecurityAuthority(
                        policies,
                        grantStore,
                        grantIds,
                        timeProvider,
                        permissionOptions,
                        provider.GetRequiredService<IApprovalBroker>(),
                        provider.GetRequiredService<IIdentifierGenerator<ApprovalRequestId>>(),
                        provider.GetRequiredService<ISecurityAuditDispatcher>(),
                        logger);
            });
            services.TryAddSingleton<ISecurityAuthoritySelector, DefaultSecurityAuthoritySelector>();
            services.TryAddSingleton<ISecurityProfilePublicationReader, DefaultSecurityProfilePublicationReader>();
            services.TryAddSingleton<ISecurityProfileSelector, DefaultSecurityProfileSelector>();
            services.TryAddSingleton<ISecurityAuditDispatcher, DefaultSecurityAuditDispatcher>();
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

        /// <summary>
        /// Registers one authority binding under the exact key that an authorization context must capture to
        /// select it, resolving this collection's own unkeyed <see cref="ISecurityAuthority"/> singleton the
        /// first time the binding is activated.
        /// </summary>
        /// <param name="authorityKey">The non-default key that identifies the authority binding.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="authorityKey"/> is blank.</exception>
        /// <remarks>
        /// Unlike the sibling overload that binds a key to an already-built <see cref="ISecurityAuthority"/>
        /// instance, this overload needs no separately built provider: it registers a factory that calls
        /// <c>IServiceProvider.GetRequiredService&lt;ISecurityAuthority&gt;()</c> the first time this binding
        /// is resolved, which is exactly the singleton <see cref="AddAgentPermissions"/> registers. Use the
        /// instance overload instead when the host owns an authority built outside this collection.
        /// </remarks>
        public IServiceCollection AddSecurityAuthority(ComponentKey<ISecurityAuthority> authorityKey)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(authorityKey.Value, nameof(authorityKey));
            _ = services.AddSingleton(provider =>
                new SecurityAuthorityBinding(authorityKey, provider.GetRequiredService<ISecurityAuthority>()));
            return services;
        }

        /// <summary>
        /// Registers a matching security-policy snapshot, profile publication, and keyed authority binding for
        /// one standalone agent composition in a single call.
        /// </summary>
        /// <param name="agentId">The agent this profile is published for.</param>
        /// <param name="agentDefinitionRevision">The definition revision this profile is published for.</param>
        /// <param name="configurationVersion">The effective-configuration revision this profile is published for.</param>
        /// <param name="profileKey">The named profile an agent definition selects.</param>
        /// <param name="authorityKey">The exact key an authorization context must capture to select the authority.</param>
        /// <param name="profileVersion">The published profile revision, or <see langword="null"/> to use revision one.</param>
        /// <param name="policyVersion">The positive published policy version this composition's snapshot belongs to; defaults to one.</param>
        /// <param name="configurePermissions">
        /// An optional delegate for permission settings other than <see cref="AgentPermissionOptions.PolicyVersion"/>
        /// and <see cref="AgentPermissionOptions.PolicySnapshot"/>, which this method always overwrites afterward.
        /// </param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="agentId"/> is default, or <paramref name="policyVersion"/> is not positive.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="profileKey"/> or <paramref name="authorityKey"/> is blank.</exception>
        /// <remarks>
        /// <para>
        /// Composing <see cref="SecurityAuthority"/> so that captured authorization contexts are actually
        /// accepted otherwise requires generating a <see cref="SecurityPolicySnapshotReference"/>, configuring
        /// <see cref="AgentPermissionOptions.PolicySnapshot"/> with it, building a provider once to resolve the
        /// authority it produced, and only then registering the keyed authority binding and profile publication
        /// that reference that exact snapshot — see <see cref="AgentPermissionOptions.PolicySnapshot"/>'s remarks
        /// for what happens when a step is skipped. This method performs all of it consistently: it derives one
        /// snapshot, calls <see cref="AddAgentPermissions"/> with it, binds <paramref name="authorityKey"/>
        /// through the lazy no-instance <c>AddSecurityAuthority(ComponentKey&lt;ISecurityAuthority&gt;)</c>
        /// overload so no intermediate provider is ever built, and publishes one
        /// <see cref="SecurityProfilePublication"/>
        /// referencing the same snapshot and authority key. It still registers no
        /// <see cref="ISecurityGrantStore"/> or <see cref="ISecurityPolicy"/>; the host selects those separately.
        /// </para>
        /// <para>
        /// This method is for one standalone composition publishing one profile. A host publishing several
        /// distinct profiles or authorities must compose them individually through the lower-level methods.
        /// </para>
        /// </remarks>
        public IServiceCollection AddStandaloneSecurityProfile(
            AgentId agentId,
            AgentDefinitionRevision agentDefinitionRevision,
            ConfigurationVersion configurationVersion,
            SecurityProfileKey profileKey,
            ComponentKey<ISecurityAuthority> authorityKey,
            SecurityProfileVersion? profileVersion = null,
            long policyVersion = 1,
            Action<AgentPermissionOptions>? configurePermissions = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
            ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
            ArgumentException.ThrowIfNullOrWhiteSpace(authorityKey.Value, nameof(authorityKey));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(policyVersion);

            var resolvedProfileVersion = profileVersion ?? new SecurityProfileVersion(1);
            var snapshot = new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()),
                new SecurityPolicyVersion(policyVersion),
                new ContentHash($"sha256:standalone-security-profile:{profileKey.Value}:{resolvedProfileVersion.Value}"));

            _ = services.AddAgentPermissions(options =>
            {
                configurePermissions?.Invoke(options);
                options.PolicyVersion = policyVersion;
                options.PolicySnapshot = snapshot;
            });
            _ = services.AddSecurityAuthority(authorityKey);
            _ = services.AddSecurityProfilePublication(new SecurityProfilePublication(
                agentId,
                agentDefinitionRevision,
                configurationVersion,
                profileKey,
                resolvedProfileVersion,
                snapshot,
                authorityKey));
            return services;
        }

        /// <summary>Adds the illustrative <see cref="WorkspaceScopedFileAccessPolicy"/> as an additive security policy.</summary>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// <see cref="ISecurityPolicy"/> registrations are additive by design, so repeating this exact registration is
        /// idempotent rather than replaceable: calling it twice still registers exactly one instance. This policy is
        /// an example, not a complete file-access policy; see its own remarks for what it does and does not decide.
        /// </remarks>
        public IServiceCollection AddWorkspaceScopedFileAccessPolicy()
        {
            ArgumentNullException.ThrowIfNull(services);
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(ISecurityPolicy)
                    && descriptor.ImplementationType == typeof(WorkspaceScopedFileAccessPolicy)))
            {
                services.Add(ServiceDescriptor.Singleton<ISecurityPolicy, WorkspaceScopedFileAccessPolicy>());
            }
            return services;
        }

        /// <summary>Adds <see cref="AllowAllSecurityPolicy"/> as an additive security policy.</summary>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// <see cref="ISecurityPolicy"/> registrations are additive by design, so repeating this exact registration
        /// is idempotent rather than replaceable: calling it twice still registers exactly one instance. See
        /// <see cref="AllowAllSecurityPolicy"/>'s own remarks for why this is appropriate only for a local,
        /// single-tenant composition.
        /// </remarks>
        public IServiceCollection AddAllowAllSecurityPolicy()
        {
            ArgumentNullException.ThrowIfNull(services);
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(ISecurityPolicy)
                    && descriptor.ImplementationType == typeof(AllowAllSecurityPolicy)))
            {
                services.Add(ServiceDescriptor.Singleton<ISecurityPolicy, AllowAllSecurityPolicy>());
            }
            return services;
        }
    }
}
