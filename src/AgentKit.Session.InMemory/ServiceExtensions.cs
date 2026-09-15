// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for the deterministic in-memory
/// session store.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="InMemorySessionStore"/> as the singular
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
        public IServiceCollection AddInMemorySessionStore()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<BranchId>>(
                _ => new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                _ => new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)));
            services.TryAddSingleton<ISessionStore, InMemorySessionStore>();

            return services;
        }

        /// <summary>Registers the protected process-local session directory with an explicit consuming component identity.</summary>
        /// <param name="securityAudience">The nonblank identity that will consume directory-specific grants.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>The registration does not create an audit dispatcher or grant store. Composition must provide those required security boundaries before resolving the directory.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
        public IServiceCollection AddInMemorySessionDirectory(ComponentId securityAudience)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                _ => new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)));
            services.TryAddSingleton<ISessionDirectory>(provider => new InMemorySessionDirectory(
                securityAudience,
                provider.GetRequiredService<ISecurityAuditDispatcher>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<InMemorySessionDirectory>>()));

            return services;
        }
    }
}
