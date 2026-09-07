// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Registers artifact coordination without fabricating a storage backend.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the default coordinator and validated mechanics.</summary>
        /// <param name="configure">Optional mechanics and logical profile configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddAgentArtifacts(Action<AgentArtifactOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<AgentArtifactOptions>()
                .Validate(static value => value.MaximumArtifactBytes > 0 && value.CopyBufferBytes > 0 && value.PreparationLifetime > TimeSpan.Zero, "Artifact mechanics must be positive.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IIdentifierGenerator<ArtifactId>, GuidArtifactIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<ArtifactPreparationId>, GuidArtifactPreparationIdGenerator>();
            services.TryAddSingleton<IArtifactCoordinator, DefaultArtifactCoordinator>();
            services.TryAddSingleton<IProcessOutputArtifactSink, ArtifactProcessOutputSink>();
            return services;
        }
    }
}
