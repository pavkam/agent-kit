// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Registers OpenTelemetry-backed run-event and security-audit sinks.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds one keyed OpenTelemetry observation exporter registration.</summary>
        /// <param name="key">The stable exporter identity.</param>
        /// <param name="configure">Configures the mutable options copied into an immutable snapshot.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The same exporter key was already registered.</exception>
        public IServiceCollection AddOpenTelemetryObservability(
            ObservationExporterKey key,
            Action<OpenTelemetryObservationOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            _ = services.AddAgentKitObservability();
            return OpenTelemetryObservationRegistration.Add(services, key, configure);
        }
    }
}
