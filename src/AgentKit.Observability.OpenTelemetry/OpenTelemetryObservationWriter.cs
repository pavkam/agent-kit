// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

using System.Text;

/// <summary>Translates neutral AgentKit records into OpenTelemetry activities, metrics, logs, and audit exports.</summary>
/// <remarks>
/// <para>
/// A writer never mints domain events and never feeds a decision back into the run. Cancellation always propagates.
/// A best-effort run-event sink contains every other failure; a required one rethrows it so the publisher records
/// recovery-required settlement. The audit writer always rethrows exporter failure because the permissions dispatcher, not
/// the sink, owns the required-versus-best-effort decision.
/// </para>
/// <para>
/// Spans, metrics, and logs carry only structural facts. Captured content reaches a span tag only after
/// <see cref="OpenTelemetryContentCapture"/> returned a policy-compliant redaction result.
/// </para>
/// </remarks>
internal static class OpenTelemetryObservationWriter
{
    /// <summary>Exports one run event.</summary>
    /// <param name="activities">The activity source spans are started on.</param>
    /// <param name="instruments">The counters recorded on the shared meter.</param>
    /// <param name="redactor">The effective redactor, consulted only when content capture is enabled.</param>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="options">The exporter's immutable snapshot.</param>
    /// <param name="runEvent">The immutable event.</param>
    /// <param name="cancellationToken">Cancels redaction and delivery.</param>
    /// <returns>Completion of the export attempt.</returns>
    /// <exception cref="ArgumentNullException">A collaborator or the event is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="Exception">A required sink's export failed; best-effort sinks contain the failure.</exception>
    internal static async ValueTask PublishRunEventAsync(
        ActivitySource activities,
        OpenTelemetryObservationInstruments instruments,
        IObservationRedactor redactor,
        ILogger logger,
        OpenTelemetryObservationOptionsSnapshot options,
        RunEvent runEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var key = options.ExporterKey.Value;
        var kind = EventKind(runEvent);
        Activity? activity = null;
        try
        {
            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Activities))
            {
                activity = StartRunEventActivity(activities, options, runEvent, kind);
                await CaptureContentAsync(activity, redactor, instruments, logger, options, runEvent, cancellationToken).ConfigureAwait(false);
                _ = activity?.SetStatus(ActivityStatusCode.Ok);
            }

            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Metrics))
            {
                instruments.RunEvent(key, kind, "exported");
            }

            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Logs))
            {
                OpenTelemetryObservationLog.RunEventExported(logger, key, runEvent.RunId, runEvent.Sequence, kind);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().Name;
            Contain(() =>
            {
                activity.SetFailed("failed", errorType);
                if (options.Signals.HasFlag(OpenTelemetrySignalSet.Metrics))
                {
                    instruments.RunEvent(key, kind, "failed");
                }

                OpenTelemetryObservationLog.RunEventExportFailed(logger, key, errorType);
            });
            if (options.Delivery.Required)
            {
                throw;
            }
        }
        finally
        {
            Contain(() => activity?.Dispose());
        }
    }

    /// <summary>Exports one security audit record through the host's audit exporter.</summary>
    /// <param name="activities">The activity source spans are started on.</param>
    /// <param name="instruments">The counters recorded on the shared meter.</param>
    /// <param name="exporter">The host-provided audit exporter.</param>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="options">The exporter's immutable snapshot.</param>
    /// <param name="record">The pre-redacted audit record.</param>
    /// <param name="cancellationToken">Cancels the export wait.</param>
    /// <returns>Completion of the exporter's bounded write attempt.</returns>
    /// <exception cref="ArgumentNullException">A collaborator or the record is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="Exception">The audit exporter failed; the permissions dispatcher normalizes the failure.</exception>
    internal static async ValueTask WriteAuditAsync(
        ActivitySource activities,
        OpenTelemetryObservationInstruments instruments,
        IOpenTelemetryAuditExporter exporter,
        ILogger logger,
        OpenTelemetryObservationOptionsSnapshot options,
        SecurityAuditRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(instruments);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var key = options.ExporterKey.Value;
        var kind = record.EventKind.ToString();
        Activity? activity = null;
        Contain(() =>
        {
            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Activities))
            {
                activity = activities.StartActivity(
                    AgentKitActivityNames.ObservationAuditExport,
                    ActivityKind.Internal,
                    parentContext: default,
                    tags: new ActivityTagsCollection
                    {
                        { AgentKitTagNames.ObservationExporterKey, key },
                        { AgentKitTagNames.ObservationExporterVersion, options.ExporterVersion.Value },
                        { AgentKitTagNames.SecurityAuditRecordId, record.Id.ToString() },
                        { AgentKitTagNames.SecurityAuditEventKind, kind },
                        { AgentKitTagNames.SecurityAuditOutcome, record.Outcome.ToString() },
                    });
            }
        });
        try
        {
            await exporter.WriteAsync(record, cancellationToken).ConfigureAwait(false);
            Contain(() =>
            {
                activity.SetSuccessful("exported");
                if (options.Signals.HasFlag(OpenTelemetrySignalSet.Metrics))
                {
                    instruments.AuditRecord(key, kind, "exported");
                }

                if (options.Signals.HasFlag(OpenTelemetrySignalSet.Logs))
                {
                    OpenTelemetryObservationLog.AuditRecordExported(logger, key, record.Id, kind);
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Contain(() => activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().Name;
            Contain(() =>
            {
                activity.SetFailed("failed", errorType);
                if (options.Signals.HasFlag(OpenTelemetrySignalSet.Metrics))
                {
                    instruments.AuditRecord(key, kind, "failed");
                }

                OpenTelemetryObservationLog.AuditExportFailed(logger, key, record.Id, errorType);
            });
            throw;
        }
        finally
        {
            Contain(() => activity?.Dispose());
        }
    }

    private static Activity? StartRunEventActivity(
        ActivitySource activities,
        OpenTelemetryObservationOptionsSnapshot options,
        RunEvent runEvent,
        string kind)
    {
        var tags = new ActivityTagsCollection
        {
            { AgentKitTagNames.ObservationExporterKey, options.ExporterKey.Value },
            { AgentKitTagNames.ObservationExporterVersion, options.ExporterVersion.Value },
            { AgentKitTagNames.ObservationEventKind, kind },
            { AgentKitTagNames.ObservationEventSequence, runEvent.Sequence },
            { AgentKitTagNames.AgentId, runEvent.AgentId.ToString() },
            { AgentKitTagNames.SessionId, runEvent.SessionId.ToString() },
            { AgentKitTagNames.RunId, runEvent.RunId.ToString() },
        };
        if (runEvent.ConversationId is { } conversationId)
        {
            tags.Add(AgentKitTagNames.ConversationId, conversationId.ToString());
        }

        if (runEvent.TurnId is { } turnId)
        {
            tags.Add(AgentKitTagNames.TurnId, turnId.ToString());
        }

        return activities.StartActivity(
            AgentKitActivityNames.ObservationRunEventExport, ActivityKind.Internal, parentContext: default, tags: tags);
    }

    private static async ValueTask CaptureContentAsync(
        Activity? activity,
        IObservationRedactor redactor,
        OpenTelemetryObservationInstruments instruments,
        ILogger logger,
        OpenTelemetryObservationOptionsSnapshot options,
        RunEvent runEvent,
        CancellationToken cancellationToken)
    {
        if (!options.ContentCapture.Enabled || OpenTelemetryContentCapture.Extract(runEvent, options) is not { } content)
        {
            return;
        }

        var outcome = await OpenTelemetryContentCapture.CaptureAsync(redactor, options, content, cancellationToken).ConfigureAwait(false);
        var kind = content.Kind.ToString();
        if (outcome.Content is { } redacted)
        {
            _ = activity?.SetTag(AgentKitTagNames.ObservationContentKind, redacted.Kind.ToString());
            _ = activity?.SetTag(AgentKitTagNames.ObservationContentClassification, redacted.Classification.ToString());
            _ = activity?.SetTag(AgentKitTagNames.ObservationContentFingerprint, redacted.Fingerprint.Value);
            _ = activity?.SetTag(AgentKitTagNames.ObservationContentValue, Encoding.UTF8.GetString(redacted.Value.AsSpan()));
            return;
        }

        var reason = outcome.OmittedReason ?? OpenTelemetryContentCapture.ReasonRedactorOmitted;
        _ = activity?.SetTag(AgentKitTagNames.ObservationContentKind, kind);
        _ = activity?.SetTag(AgentKitTagNames.ObservationContentOmittedReason, reason);
        Contain(() =>
        {
            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Metrics))
            {
                instruments.ContentOmitted(options.ExporterKey.Value, kind, reason);
            }

            if (options.Signals.HasFlag(OpenTelemetrySignalSet.Logs))
            {
                OpenTelemetryObservationLog.ContentOmitted(logger, options.ExporterKey.Value, kind, reason);
            }
        });
    }

    private static string EventKind(RunEvent runEvent) =>
        runEvent switch
        {
            ContentDeltaEvent => "content_delta",
            MessageCommittedEvent => "message_committed",
            _ => "other",
        };

    private static void Contain(Action observation)
    {
        try
        {
            observation();
        }
        catch (Exception)
        {
            // Failure while recording a failure never replaces the original outcome.
        }
    }
}
