// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the Microsoft logging foundation used by AgentKit instrumentation.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers replaceable Microsoft logging services without adding an exporter.</summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Registration is additive and idempotent. Hosts remain responsible for
        /// filters, providers, activity listeners, meter listeners, and exporters.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddAgentKitObservability()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddLogging();
            return services;
        }
    }
}
