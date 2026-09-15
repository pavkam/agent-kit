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
        /// Registers <see cref="SqliteSessionStore"/> as the singular
        /// <see cref="ISessionStore"/>, along with default GUID-based
        /// generators for <see cref="BranchId"/> and <see cref="SecurityAuditRecordId"/>.
        /// </summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: uses <c>TryAdd</c> semantics, so calling this more
        /// than once, or alongside another store registration that already
        /// claimed <see cref="ISessionStore"/>, keeps whichever registration
        /// happened first.
        /// </remarks>
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
            services.TryAddSingleton(target);
            services.TryAddSingleton(settings);
            services.TryAddSingleton<ISessionStore, SqliteSessionStore>();

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
