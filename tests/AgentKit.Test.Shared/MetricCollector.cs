// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

/// <summary>Collects measurements from one named instrument on the shared AgentKit meter.</summary>
public sealed class MetricCollector: IDisposable
{
    private readonly ConcurrentQueue<MetricObservation> _observations = new();
    private readonly MeterListener _listener = new();
    private readonly string _instrumentName;

    /// <summary>Starts observing one instrument on the shared AgentKit meter.</summary>
    /// <param name="instrumentName">The exact instrument name to capture.</param>
    /// <param name="meterName">The meter name, defaulting to the shared AgentKit meter.</param>
    /// <exception cref="ArgumentException"><paramref name="instrumentName"/> is blank.</exception>
    public MetricCollector(string instrumentName, string meterName = AgentKitDiagnostics.MeterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrumentName);
        _instrumentName = instrumentName;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == _instrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>(RecordLong);
        _listener.SetMeasurementEventCallback<double>(RecordDouble);
        _listener.Start();
    }

    /// <summary>Returns a stable snapshot of captured measurements in callback order.</summary>
    public IReadOnlyList<MetricObservation> Snapshot() => _observations.ToArray();

    /// <inheritdoc/>
    public void Dispose() => _listener.Dispose();

    private void RecordLong(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? _) =>
        Record(instrument, measurement, null, tags);

    private void RecordDouble(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? _) =>
        Record(instrument, null, measurement, tags);

    private void Record(Instrument instrument, long? longValue, double? doubleValue, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var tagMap = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            tagMap[tag.Key] = tag.Value;
        }

        _observations.Enqueue(new MetricObservation(instrument.Name, tagMap, longValue, doubleValue));
    }
}
