// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Delivers run events to OpenTelemetry without becoming a semantic control dependency.</summary>
/// <remarks>
/// The sink is a stateless, thread-safe singleton closed over exactly one exporter key's immutable snapshot. It neither
/// mints events nor influences the run; a best-effort registration contains export failure and a required one surfaces it
/// to the publisher.
/// </remarks>
internal sealed class OpenTelemetryRunEventSink: IRunEventSink
{
    private readonly ActivitySource _activities;
    private readonly OpenTelemetryObservationInstruments _instruments;
    private readonly IObservationRedactor _redactor;
    private readonly ILogger _logger;
    private readonly OpenTelemetryObservationOptionsSnapshot _options;

    /// <summary>Initializes the sink for one exporter key.</summary>
    /// <param name="activities">The activity source spans are started on.</param>
    /// <param name="meter">The meter the sink's counters are created on.</param>
    /// <param name="redactor">The effective redactor.</param>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="options">The exporter's immutable snapshot.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">Content capture is enabled but <paramref name="redactor"/> is the omission-only default.</exception>
    internal OpenTelemetryRunEventSink(
        ActivitySource activities,
        Meter meter,
        IObservationRedactor redactor,
        ILogger<OpenTelemetryRunEventSink> logger,
        OpenTelemetryObservationOptionsSnapshot options)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentNullException.ThrowIfNull(redactor);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ContentCapture.Enabled && redactor is OmissionOnlyObservationRedactor)
        {
            throw new InvalidOperationException(
                $"Content capture is enabled for observation exporter '{options.ExporterKey.Value}' but only the omission-only redactor is registered. Register an explicit IObservationRedactor.");
        }

        _activities = activities;
        _instruments = new OpenTelemetryObservationInstruments(meter);
        _redactor = redactor;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc/>
    public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runEvent);
        return OpenTelemetryObservationWriter.PublishRunEventAsync(
            _activities, _instruments, _redactor, _logger, _options, runEvent, cancellationToken);
    }
}
