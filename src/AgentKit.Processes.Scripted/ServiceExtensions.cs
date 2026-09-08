// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Scripted;

/// <summary>Registers the deterministic no-host process implementation.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one scripted resolver and runner while preserving earlier replacements.</summary>
        /// <param name="configure">The required executable mappings and operation scenarios.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        public IServiceCollection AddScriptedProcesses(Action<ScriptedProcessOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            _ = services.AddAgentKitObservability();
            _ = services.AddOptions<ScriptedProcessOptions>()
                .Configure(configure)
                .Validate(static options => Path.IsPathRooted(options.WorkspaceRoot), "WorkspaceRoot must be absolute.")
                .Validate(static options => options.Executables.Count > 0, "At least one executable mapping is required.")
                .ValidateOnStart();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IProcessIntentResolver, ScriptedProcessIntentResolver>();
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddSingleton<IProcessRunner>(static provider => new ScriptedProcessRunner(
                provider.GetRequiredService<IProcessIntentResolver>(),
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IOptions<ScriptedProcessOptions>>(),
                provider.GetService<ILogger<ScriptedProcessRunner>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>()));
            return services;
        }
    }
}
