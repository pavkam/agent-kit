// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Delivers security audit records to a host-provided OpenTelemetry audit exporter.</summary>
/// <remarks>
/// Audit records are pre-redacted by the security architecture, so this sink needs no redactor and exports no free-form
/// content. Exporter failure is rethrown unchanged: the permissions audit dispatcher decides whether it is isolated or
/// prevents the protected operation, and this sink never acquires control authority.
/// </remarks>
internal sealed class OpenTelemetrySecurityAuditSink: ISecurityAuditSink
{
    private readonly ActivitySource _activities;
    private readonly OpenTelemetryObservationInstruments _instruments;
    private readonly IOpenTelemetryAuditExporter _exporter;
    private readonly ILogger _logger;
    private readonly OpenTelemetryObservationOptionsSnapshot _options;

    /// <summary>Initializes the sink for one exporter key.</summary>
    /// <param name="activities">The activity source spans are started on.</param>
    /// <param name="meter">The meter the sink's counters are created on.</param>
    /// <param name="exporter">The host-provided audit exporter.</param>
    /// <param name="logger">The content-free logger.</param>
    /// <param name="options">The exporter's immutable snapshot.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal OpenTelemetrySecurityAuditSink(
        ActivitySource activities,
        Meter meter,
        IOpenTelemetryAuditExporter exporter,
        ILogger<OpenTelemetrySecurityAuditSink> logger,
        OpenTelemetryObservationOptionsSnapshot options)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentNullException.ThrowIfNull(exporter);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        _activities = activities;
        _instruments = new OpenTelemetryObservationInstruments(meter);
        _exporter = exporter;
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc/>
    public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        return OpenTelemetryObservationWriter.WriteAuditAsync(
            _activities, _instruments, _exporter, _logger, _options, record, cancellationToken);
    }
}
