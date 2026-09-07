// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Registers deterministic process-local artifact storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the in-memory store when no artifact backend was selected.</summary>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddInMemoryArtifactStore()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IArtifactStore, InMemoryArtifactStore>();
            return services;
        }
    }
}
