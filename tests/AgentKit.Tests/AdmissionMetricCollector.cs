// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using AgentKit.Observability;

/// <summary>Collects bounded admission-counter outcomes for one test lifetime.</summary>
internal sealed class AdmissionMetricCollector: IDisposable
{
    private readonly ConcurrentQueue<string> _outcomes = new();
    private readonly MeterListener _listener = new();

    /// <summary>Starts observing the shared admission counter.</summary>
    internal AdmissionMetricCollector()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                && instrument.Name == AgentKitMetricNames.AgentAdmissionCount)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == AgentKitTagNames.Outcome && tag.Value is string outcome)
                {
                    _outcomes.Enqueue(outcome);
                }
            }
        });
        _listener.Start();
    }

    /// <summary>Gets a stable snapshot of recorded outcomes.</summary>
    internal IReadOnlyList<string> Snapshot() => _outcomes.ToArray();

    /// <summary>Stops observing metrics.</summary>
    public void Dispose() => _listener.Dispose();
}
