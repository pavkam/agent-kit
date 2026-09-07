// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Registers first-party input/output coordination components.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the protected human-question broker over an application-provided channel.</summary>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddHumanQuestionBroker()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IHumanQuestionBroker, DefaultHumanQuestionBroker>();
            return services;
        }
    }
}
