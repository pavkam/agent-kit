// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

using AgentKit.IO;
using AgentKit.Permissions;

/// <summary>Validates one keyed exporter registration and installs its run-event and audit sinks through the public IO and permissions registrations.</summary>
/// <remarks>
/// Registration keeps no static state: the captured snapshot is recorded in the service collection as an
/// <see cref="OpenTelemetryObservationDeclaration"/>, so separate collections never interfere. Repeating an identical
/// registration is idempotent; reusing a key for different options fails.
/// </remarks>
internal static class OpenTelemetryObservationRegistration
{
    private const string _sinkNamePrefix = "opentelemetry";

    /// <summary>Adds one keyed exporter registration and its immutable snapshot.</summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="key">The stable exporter key.</param>
    /// <param name="configure">Configures the mutable options copied into the snapshot.</param>
    /// <returns>The same collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or the configured options are invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured value is outside its valid range.</exception>
    /// <exception cref="InvalidOperationException">The key is already registered with different options.</exception>
    internal static IServiceCollection Add(
        IServiceCollection services,
        ObservationExporterKey key,
        Action<OpenTelemetryObservationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));

        var options = new OpenTelemetryObservationOptions();
        configure(options);
        var snapshot = Capture(key, options);

        var existing = services
            .Select(static descriptor => descriptor.ImplementationInstance as OpenTelemetryObservationDeclaration)
            .FirstOrDefault(declaration => declaration is not null && declaration.Snapshot.ExporterKey == key);
        if (existing is not null)
        {
            return existing.Snapshot.Equals(snapshot)
                ? services
                : throw new InvalidOperationException(
                    $"An OpenTelemetry observation exporter is already registered for key '{key.Value}' with different options.");
        }

        _ = services.AddSingleton(new OpenTelemetryObservationDeclaration(snapshot));
        var runSignals = OpenTelemetrySignalSet.Activities | OpenTelemetrySignalSet.Metrics | OpenTelemetrySignalSet.Logs;
        if ((snapshot.Signals & runSignals) != 0)
        {
            _ = services.AddRunEventSink(
                new RunEventSinkRegistration(
                    $"{_sinkNamePrefix}.run.{key.Value}",
                    snapshot.Delivery.Required ? RunEventDelivery.Required : RunEventDelivery.BestEffort,
                    order: 0,
                    snapshot.Delivery.FlushDeadline),
                provider => new OpenTelemetryRunEventSink(
                    AgentKitDiagnostics.Activities,
                    AgentKitDiagnostics.Metrics,
                    provider.GetRequiredService<IObservationRedactor>(),
                    provider.GetRequiredService<ILogger<OpenTelemetryRunEventSink>>(),
                    snapshot));
        }

        if (snapshot.Signals.HasFlag(OpenTelemetrySignalSet.Audit))
        {
            _ = services.AddSecurityAuditSink(
                new SecurityAuditSinkRegistration(
                    [.. Enum.GetValues<SecurityAuditEventKind>()],
                    snapshot.Delivery.Required ? SecurityAuditDelivery.Required : SecurityAuditDelivery.BestEffort,
                    snapshot.AuditExporterProvidesDurableAcceptance),
                provider => new OpenTelemetrySecurityAuditSink(
                    AgentKitDiagnostics.Activities,
                    AgentKitDiagnostics.Metrics,
                    provider.GetService<IOpenTelemetryAuditExporter>()
                        ?? throw new InvalidOperationException(
                            $"Observation exporter '{key.Value}' enables the audit signal but no IOpenTelemetryAuditExporter is registered."),
                    provider.GetRequiredService<ILogger<OpenTelemetrySecurityAuditSink>>(),
                    snapshot));
        }

        return services;
    }

    private static OpenTelemetryObservationOptionsSnapshot Capture(ObservationExporterKey key, OpenTelemetryObservationOptions options)
    {
        Validate(options);
        return new OpenTelemetryObservationOptionsSnapshot(
            key,
            new ObservationExporterVersion(options.ExporterVersion),
            options.Signals,
            options.ContentCapture,
            options.ContentClassification,
            options.AllowedClassifications,
            options.Delivery,
            options.Bounds,
            options.AuditExporterProvidesDurableAcceptance);
    }

    private static void Validate(OpenTelemetryObservationOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ExporterVersion, nameof(options));
        ArgumentNullException.ThrowIfNull(options.ContentCapture);
        ArgumentNullException.ThrowIfNull(options.AllowedClassifications);
        ArgumentNullException.ThrowIfNull(options.Delivery);
        ArgumentNullException.ThrowIfNull(options.Bounds);
        ArgumentOutOfRangeException.ThrowIfUndefined(options.ContentClassification, nameof(options));
        foreach (var classification in options.AllowedClassifications)
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(classification, nameof(options));
        }

        const OpenTelemetrySignalSet all = OpenTelemetrySignalSet.Activities | OpenTelemetrySignalSet.Metrics
            | OpenTelemetrySignalSet.Logs | OpenTelemetrySignalSet.Audit;
        if (options.Signals == OpenTelemetrySignalSet.None || (options.Signals & ~all) != 0)
        {
            throw new ArgumentException("At least one defined OpenTelemetry signal must be enabled.", nameof(options));
        }

        if (options.ContentCapture.Enabled && !options.AllowedClassifications.Contains(options.ContentClassification))
        {
            throw new ArgumentException(
                "Content capture requires a classification policy that allows the declared content classification.",
                nameof(options));
        }

        if (options.ContentCapture.Enabled && !options.Signals.HasFlag(OpenTelemetrySignalSet.Activities))
        {
            throw new ArgumentException(
                "Content capture exports content as span data and therefore requires the activities signal.",
                nameof(options));
        }

        if (options.Delivery.Required
            && options.Signals.HasFlag(OpenTelemetrySignalSet.Audit)
            && !options.AuditExporterProvidesDurableAcceptance)
        {
            throw new ArgumentException(
                "Required audit delivery needs an audit exporter that provides durable acceptance; set AuditExporterProvidesDurableAcceptance.",
                nameof(options));
        }
    }
}
