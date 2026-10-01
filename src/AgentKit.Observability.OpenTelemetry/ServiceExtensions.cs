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
        /// <param name="configure">Configures the mutable options copied into an immutable per-key snapshot.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, or the configured options are invalid (no signal, capture without an allowed classification or the activities signal, or required audit delivery without a durable audit exporter).</exception>
        /// <exception cref="ArgumentOutOfRangeException">The exporter version or another configured value is out of range.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered with different options.</exception>
        /// <remarks>
        /// <para>
        /// The registration is additive and keyed. It calls <c>AddRunEventSink</c> (for the activities, metrics, and logs
        /// signals) and <c>AddSecurityAuditSink</c> (for the audit signal) with factories closed over this key's immutable
        /// snapshot, so several exporter keys never share options. Repeating an identical registration is idempotent;
        /// reusing a key with different options throws. No exporter is registered by default: the audit signal requires the
        /// host to register an <see cref="IOpenTelemetryAuditExporter"/>, and the activity, meter, and logging providers
        /// remain host-owned.
        /// </para>
        /// <para>
        /// Registration also adds the shared logging foundation and the omission-only <see cref="IObservationRedactor"/> by
        /// <c>TryAdd</c>. Enabling content capture requires an explicit replacement redactor; resolving a sink with capture
        /// enabled and only the omission-only default throws <see cref="InvalidOperationException"/>.
        /// </para>
        /// </remarks>
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
