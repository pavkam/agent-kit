// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Delivers run events to OpenTelemetry without becoming a semantic control dependency.</summary>
internal sealed class OpenTelemetryRunEventSink(
    IObservationRedactor redactor,
    OpenTelemetryObservationOptionsSnapshot options): IRunEventSink
{
    private readonly IObservationRedactor _redactor = redactor;
    private readonly OpenTelemetryObservationOptionsSnapshot _options = options;

    /// <inheritdoc/>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) =>
        OpenTelemetryObservationWriter.PublishRunEventAsync(
            AgentKitDiagnostics.Activities,
            AgentKitDiagnostics.Metrics,
            _redactor,
            _options,
            runEvent,
            cancellationToken);
}
