// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for the first-party hook dispatcher.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultHookDispatcher"/> as the singular
        /// <see cref="IHookDispatcher"/>.
        /// </summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: uses <c>TryAdd</c> semantics, so calling this more
        /// than once keeps the first registration. This method does not
        /// register any concrete hook implementations; applications and
        /// feature packages register their own hooks additively against
        /// whichever hook interface their point defines.
        /// </remarks>
        public IServiceCollection AddAgentHooks()
        {
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton<IHookDispatcher, DefaultHookDispatcher>();
            return services;
        }
    }
}
