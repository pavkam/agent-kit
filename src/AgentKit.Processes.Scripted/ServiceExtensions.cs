// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Registers the deterministic no-host process implementation.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed scripted executable resolver and process executor profile.</summary>
        /// <param name="key">The executor profile key authored by the application.</param>
        /// <param name="configure">The required executable mappings and operation scenarios.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        public IServiceCollection AddAgentScriptedProcesses(
            ProcessExecutorKey key,
            Action<ScriptedProcessOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            return AgentScriptedProcessRegistration.Add(services, key, configure);
        }
    }
}
