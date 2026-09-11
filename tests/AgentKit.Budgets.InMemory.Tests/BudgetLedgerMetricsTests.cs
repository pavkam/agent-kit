// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory.Tests;



/// <summary>Verifies BudgetLedgerMetrics behavior and contracts.</summary>
public sealed class BudgetLedgerMetricsTests
{
    /// <summary>Verifies invalid metric arguments fail before publishing any measurement and identify the exact parameter.</summary>
    [Fact]
    public void BudgetLedgerMetrics_WhenArgumentsAreInvalid_RejectsBeforeMeasurement()
    {
        var measurements = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var parent = StartParent("budget-ledger-invalid-metrics");
        using var listener = ListenToMetrics(measurements, parent.TraceId);
        Should.Throw<ArgumentException>(() => BudgetLedgerMetrics.RecordCount(" ", "completed")).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentException>(() => BudgetLedgerMetrics.RecordCount("create_scope", " ")).ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentException>(() => BudgetLedgerMetrics.RecordDuration(" ", "completed", TimeSpan.Zero)).ParamName.ShouldBe("operation");
        Should.Throw<ArgumentException>(() => BudgetLedgerMetrics.RecordDuration("create_scope", " ", TimeSpan.Zero)).ParamName.ShouldBe("outcome");
        Should.Throw<ArgumentOutOfRangeException>(() => BudgetLedgerMetrics.RecordDuration("create_scope", "completed", TimeSpan.FromTicks(-1))).ParamName.ShouldBe("duration");
        measurements.ShouldBeEmpty();
    }

    private static MeterListener ListenToMetrics(List<(string Name, IReadOnlyDictionary<string, object?> Tags)> measurements, ActivityTraceId traceId)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (IsLedgerInstrument(instrument))
                {
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => RecordForTrace(traceId, measurements, instrument, tags));
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => RecordForTrace(traceId, measurements, instrument, tags));
        listener.Start();
        return listener;
    }

    private static Activity StartParent(string name) => new Activity(name).SetIdFormat(ActivityIdFormat.W3C).Start();
    private static bool IsLedgerInstrument(Instrument instrument) => instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.BudgetLedgerOperationCount or AgentKitMetricNames.BudgetLedgerOperationDuration;
    private static void RecordForTrace(ActivityTraceId traceId, List<(string Name, IReadOnlyDictionary<string, object?> Tags)> measurements, Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (Activity.Current?.TraceId == traceId)
        {
            measurements.Add((instrument.Name, tags.ToArray().ToDictionary(item => item.Key, item => item.Value)));
        }
    }
}
