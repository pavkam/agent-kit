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
        /// Adds <see cref="InMemorySessionStore"/> to the additive <see cref="ISessionStore"/>
        /// set, along with default GUID-based generators for <see cref="BranchId"/> and
        /// <see cref="SecurityAuditRecordId"/>.
        /// </summary>
        /// <param name="configure">Optional additional configuration for <see cref="InMemorySessionStoreOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// <para>
        /// The store registration is additive alongside other store packages and
        /// idempotent for this store: calling it more than once registers a single
        /// <see cref="InMemorySessionStore"/>, and a store registered earlier by another
        /// package is neither replaced nor hidden. Registration order never selects a
        /// store; the session directory route and store key do. Supporting generators
        /// and the clock use <c>TryAdd</c> semantics so hosts may replace them.
        /// </para>
        /// <para>
        /// Options configuration is additive: every non-null <paramref name="configure"/>
        /// delegate is appended to the <see cref="InMemorySessionStoreOptions"/> pipeline
        /// and runs in registration order. The options are validated when first
        /// resolved and eagerly at host start; an invalid
        /// <see cref="InMemorySessionStoreOptions.MaximumIssuedReadSnapshots"/> fails
        /// with <see cref="OptionsValidationException"/> before the store is constructed.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddInMemorySessionStore(Action<InMemorySessionStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();

            var optionsBuilder = services.AddOptions<InMemorySessionStoreOptions>()
                .Validate(static o => o.MaximumIssuedReadSnapshots > 0, "MaximumIssuedReadSnapshots must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<BranchId>>(
                _ => new GuidIdentifierGenerator<BranchId>(static value => new BranchId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
                _ => new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)));
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionStore, InMemorySessionStore>());

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
