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
            return services;
        }
    }
}
