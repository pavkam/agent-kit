// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

using System.Diagnostics;

/// <summary>Translates neutral AgentKit records into OpenTelemetry activities, metrics, logs, and audit exports.</summary>
internal static class OpenTelemetryObservationWriter
{
    private static readonly Counter<long> _runEventCounter =
        AgentKitDiagnostics.Metrics.CreateCounter<long>("agentkit.observability.run_event.count");

    /// <summary>Publishes one run event without mutating semantic run state when export fails.</summary>
    internal static ValueTask PublishRunEventAsync(
        ActivitySource activities,
        Meter meter,
        IObservationRedactor redactor,
        OpenTelemetryObservationOptionsSnapshot options,
        RunEvent runEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runEvent);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Activities))
            {
                using var scope = AgentKitActivityScope.Start(
                    AgentKitActivityNames.RunEventHub,
                    ActivityKind.Internal,
                    new ActivityTagsCollection
                    {
                        { AgentKitTagNames.RunId, runEvent.RunId.ToString() },
                        { AgentKitTagNames.Outcome, runEvent.GetType().Name },
                    });
                _ = (scope.Activity?.SetStatus(ActivityStatusCode.Ok));
            }

            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Metrics))
            {
                _runEventCounter.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "accepted"));
            }
        }
        catch (Exception)
        {
            // Export failure is observational only.
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Writes one audit record after optional redaction without changing authorization outcomes.</summary>
    internal static async ValueTask WriteAuditAsync(
        IOpenTelemetryAuditExporter exporter,
        IObservationRedactor redactor,
        OpenTelemetryObservationOptionsSnapshot options,
        SecurityAuditRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        if (!options.Signals.HasFlag(OpenTelemetrySignalSet.Audit))
        {
            return;
        }

        try
        {
            await exporter.WriteAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Audit export failure is handled by the permissions dispatcher policy.
        }
    }
}
