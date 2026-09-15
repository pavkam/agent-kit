// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

using AgentKit.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for durable host-local SQLite session storage and discovery.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds <see cref="SqliteSessionStore"/> bound to <paramref name="target"/> to the
        /// additive <see cref="ISessionStore"/> set, along with the first-party portable entry
        /// codecs and default GUID-based generators for <see cref="BranchId"/> and
        /// <see cref="SecurityAuditRecordId"/>.
        /// </summary>
        /// <param name="target">The explicit fixed database target captured for the store.</param>
        /// <param name="settings">
        /// Optional validated operational bounds captured for the store. When <see langword="null"/>,
        /// <see cref="SqliteSessionStoreSettings.CreateDefault"/> is used.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// The store registration is additive alongside other store packages and
        /// idempotent for this store: repeated calls register a single
        /// <see cref="SqliteSessionStore"/> bound to the first captured target and settings,
        /// and a store registered earlier by another package is neither replaced nor hidden.
        /// Registration order never selects a store; the session directory route and store
        /// key do. The target and settings are captured in the store factory rather than
        /// published as ambient singletons. Codecs, generators, and the clock use
        /// <c>TryAdd</c> semantics so hosts may replace them.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        public IServiceCollection AddSqliteSessionStore(
            SqliteSessionStoreTarget target,
            SqliteSessionStoreSettings? settings = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            settings ??= SqliteSessionStoreSettings.CreateDefault();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, ExecutionLaneProvisionedSessionEntryCodec>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, InputPromotedSessionEntryCodec>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, OperationAcceptedSessionEntryCodec>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, MessageSessionEntryCodec>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, InputAdmittedSessionEntryCodec>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec, CompactionSessionEntryCodec>());
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<SessionEntryCodecCatalog>();
            services.TryAddSingleton<ISessionEntryCodecCatalog>(provider =>
                provider.GetRequiredService<SessionEntryCodecCatalog>());
            services.TryAddSingleton<IIdentifierGenerator<BranchId>>(
                _ => new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                _ => new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)));
            var boundSettings = settings;
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionStore, SqliteSessionStore>(provider =>
                new SqliteSessionStore(
                    provider.GetRequiredService<IIdentifierGenerator<BranchId>>(),
                    provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                    provider.GetRequiredService<ISecurityAuditDispatcher>(),
                    provider.GetRequiredService<ISecurityGrantStore>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ISessionEntryCodecCatalog>(),
                    target,
                    boundSettings,
                    provider.GetRequiredService<ILogger<SqliteSessionStore>>())));

            return services;
        }

        /// <summary>
        /// Adds <see cref="SqliteSessionStore"/> bound to <paramref name="target"/> to the
        /// additive <see cref="ISessionStore"/> set using bounds supplied through a
        /// configure delegate over <see cref="SqliteSessionStoreOptions"/>.
        /// </summary>
        /// <param name="target">The explicit fixed SQLite database target.</param>
        /// <param name="configure">
        /// The required delegate that mutates a fresh
        /// <see cref="SqliteSessionStoreOptions"/> instance. It runs exactly
        /// once, synchronously, before any service is registered; the options
        /// object is discarded afterwards and is never registered in the container.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// The configured values are materialized into an immutable
        /// <see cref="SqliteSessionStoreSettings"/> record, so every bound is
        /// validated eagerly at registration and an invalid value leaves the
        /// service collection unchanged. The overload then behaves exactly like
        /// <see cref="AddSqliteSessionStore(IServiceCollection, SqliteSessionStoreTarget, SqliteSessionStoreSettings?)"/>,
        /// including its additive, per-store idempotent registration: a second call
        /// with different options does not rebind the already captured store, and the
        /// options are never published as a singleton. The delegate is required
        /// because an optional one would make the no-argument call ambiguous
        /// with the settings overload; call that overload when no configuration
        /// is needed.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/>, <paramref name="target"/>, or <paramref name="configure"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The configured <see cref="SqliteSessionStoreOptions"/> violate a
        /// <see cref="SqliteSessionStoreSettings"/> constraint.
        /// </exception>
        public IServiceCollection AddSqliteSessionStore(
            SqliteSessionStoreTarget target,
            Action<SqliteSessionStoreOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(configure);
            var options = new SqliteSessionStoreOptions();
            configure(options);
            return services.AddSqliteSessionStore(target, new SqliteSessionStoreSettings(
                options.LockTimeout, options.MaximumEntryPayloadBytes, options.MaximumIssuedReadSnapshots));
        }

        /// <summary>Registers the protected durable SQLite session directory with an explicit consuming component identity.</summary>
        /// <param name="securityAudience">The nonblank identity that will consume directory-specific grants.</param>
        /// <param name="target">The explicit SQLite database target shared with session storage.</param>
        /// <param name="settings">
        /// Optional finite database bounds. When <see langword="null"/>,
        /// <see cref="SqliteSessionStoreSettings.CreateDefault"/> is used.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// <para>The registration does not create an audit dispatcher or grant store. Composition must provide those required security boundaries before resolving the directory.</para>
        /// <para>
        /// Idempotent: uses <c>TryAdd</c> semantics for the singular <see cref="ISessionDirectory"/>,
        /// so the first registration wins. The target and settings are captured in the
        /// directory factory and are not published as ambient singletons; they do not feed
        /// <c>AddSqliteSessionStore</c>, which captures its own target and settings in the
        /// store factory.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
        public IServiceCollection AddSqliteSessionDirectory(ComponentId securityAudience,
            SqliteSessionStoreTarget target, SqliteSessionStoreSettings? settings = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
            ArgumentNullException.ThrowIfNull(target);
            settings ??= SqliteSessionStoreSettings.CreateDefault();
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                _ => new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)));
            services.TryAddSingleton<ISessionDirectory>(provider => new SqliteSessionDirectory(
                securityAudience,
                provider.GetRequiredService<ISecurityAuditDispatcher>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                provider.GetRequiredService<TimeProvider>(),
                target,
                settings,
                provider.GetRequiredService<ILogger<SqliteSessionDirectory>>()));

            return services;
        }

        /// <summary>
        /// Registers the protected durable SQLite session directory using bounds
        /// supplied through a configure delegate over <see cref="SqliteSessionStoreOptions"/>.
        /// </summary>
        /// <param name="securityAudience">The nonblank identity that will consume directory-specific grants.</param>
        /// <param name="target">The explicit SQLite database target shared with session storage.</param>
        /// <param name="configure">
        /// The required delegate that mutates a fresh
        /// <see cref="SqliteSessionStoreOptions"/> instance. It runs exactly
        /// once, synchronously, before any service is registered; the options
        /// object is discarded afterwards and is never registered in the container.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// The configured values are materialized into an immutable
        /// <see cref="SqliteSessionStoreSettings"/> record, so every bound is
        /// validated eagerly at registration and an invalid value leaves the
        /// service collection unchanged. The overload then behaves exactly like
        /// <see cref="AddSqliteSessionDirectory(IServiceCollection, ComponentId, SqliteSessionStoreTarget, SqliteSessionStoreSettings?)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/>, <paramref name="target"/>, or <paramref name="configure"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The configured <see cref="SqliteSessionStoreOptions"/> violate a
        /// <see cref="SqliteSessionStoreSettings"/> constraint.
        /// </exception>
        public IServiceCollection AddSqliteSessionDirectory(ComponentId securityAudience,
            SqliteSessionStoreTarget target, Action<SqliteSessionStoreOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(configure);
            var options = new SqliteSessionStoreOptions();
            configure(options);
            return services.AddSqliteSessionDirectory(securityAudience, target, new SqliteSessionStoreSettings(
                options.LockTimeout, options.MaximumEntryPayloadBytes, options.MaximumIssuedReadSnapshots));
        }
    }
}
