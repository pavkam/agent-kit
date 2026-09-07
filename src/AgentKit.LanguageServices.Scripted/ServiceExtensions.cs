// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Registers deterministic language-intelligence scenarios for tests and replay.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one scripted provider while preserving earlier explicit replacements.</summary>
        /// <param name="configure">The required deterministic scenarios.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        public IServiceCollection AddScriptedLanguageIntelligence(Action<ScriptedLanguageOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            _ = services.AddAgentKitObservability();
            _ = services.AddOptions<ScriptedLanguageOptions>().Configure(configure).ValidateOnStart();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<LanguageQueryId>, GuidLanguageQueryIdGenerator>();
            services.TryAddSingleton<ILanguageIntelligenceService, ScriptedLanguageIntelligenceService>();
            return services;
        }
    }
}
