// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

using AgentKit.IO;
using AgentKit.Permissions;

/// <summary>Registers keyed OpenTelemetry run-event and audit sinks through the public IO and permissions surfaces.</summary>
internal static class OpenTelemetryObservationRegistration
{
    private static readonly HashSet<string> _registeredKeys = new(StringComparer.Ordinal);

    /// <summary>Adds one keyed exporter registration and its immutable snapshot.</summary>
    internal static IServiceCollection Add(
        IServiceCollection services,
        ObservationExporterKey key,
        Action<OpenTelemetryObservationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        if (!_registeredKeys.Add(key.Value))
        {
            throw new InvalidOperationException($"An OpenTelemetry observation exporter is already registered for key '{key.Value}'.");
        }

        var options = new OpenTelemetryObservationOptions();
        configure(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ExporterVersion, nameof(options));
        var snapshot = new OpenTelemetryObservationOptionsSnapshot(
            key,
            new ObservationExporterVersion(options.ExporterVersion),
            options.Signals,
            options.ContentCapture,
            options.Delivery,
            options.Bounds);
        _ = services.AddSingleton(snapshot);
        services.TryAddSingleton<IOpenTelemetryAuditExporter, NullOpenTelemetryAuditExporter>();
        _ = services.AddRunEventSink<OpenTelemetryRunEventSink>(
            new RunEventSinkRegistration($"opentelemetry.run.{key.Value}", RunEventDelivery.BestEffort, order: 0));
        _ = services.AddSecurityAuditSink<OpenTelemetrySecurityAuditSink>(
            new SecurityAuditSinkRegistration(
                [.. Enum.GetValues<SecurityAuditEventKind>()],
                SecurityAuditDelivery.BestEffort,
                providesDurableAcceptance: false));
        return services;
    }
}
