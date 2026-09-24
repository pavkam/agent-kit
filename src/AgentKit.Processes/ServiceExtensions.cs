// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes;

/// <summary>Registers the root-jailed, sandbox-required operating-system process boundary.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed executable resolver and process executor profile.</summary>
        /// <param name="key">The executor profile key authored by the application.</param>
        /// <param name="configure">Profile configuration for workspace roots, executables, and defaults.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public IServiceCollection AddAgentProcesses(
            ProcessExecutorKey key,
            Action<AgentProcessOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            return AgentProcessRegistration.Add(services, key, configure);
        }

    }
}
