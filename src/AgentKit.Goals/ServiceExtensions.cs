// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Registers protected goal and delegation boundaries without selecting an application dispatcher.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the exact-grant delegation broker.</summary>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddAgentDelegation()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<ITaskDelegationBroker, DefaultTaskDelegationBroker>();
            return services;
        }
    }
}
