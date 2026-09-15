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
        /// <param name="settings">Optional finite database bounds; defaults to <see cref="SqliteSessionStoreSettings.CreateDefault"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// The store registration is additive alongside other store packages and
        /// idempotent for this store: repeated calls register a single
        /// <see cref="SqliteSessionStore"/> bound to the first captured target and settings,
        /// and a store registered earlier by another package is neither replaced nor hidden.
        /// Registration order never selects a store; the session directory route and store
        /// key do. Codecs, generators, and the clock use <c>TryAdd</c> semantics so hosts may
        /// replace them.
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

        /// <summary>Registers the protected durable SQLite session directory with an explicit consuming component identity.</summary>
        /// <param name="securityAudience">The nonblank identity that will consume directory-specific grants.</param>
        /// <param name="target">The explicit SQLite database target shared with session storage.</param>
        /// <param name="settings">Optional finite database bounds.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>The registration does not create an audit dispatcher or grant store. Composition must provide those required security boundaries before resolving the directory.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
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
    }
}
