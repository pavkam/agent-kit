// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;

using System.Diagnostics.Metrics;

/// <summary>Verifies the shared metric collector used by observability tests across packages.</summary>
public sealed class MetricCollectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenInstrumentNameIsBlank_ThrowsArgumentExceptionNamingIt(string? name)
    {
        var exception = Should.Throw<ArgumentException>(() => new MetricCollector(name!));

        exception.ParamName.ShouldBe("instrumentName");
    }

    [Fact]
    public void Snapshot_WhenOnlyTheNamedInstrumentEmits_CapturesItsTagsAndValues()
    {
        var name = $"test.collector.{Guid.NewGuid():N}.count";
        using var meter = new Meter(AgentKitDiagnostics.MeterName);
        var counter = meter.CreateCounter<long>(name);
        var other = meter.CreateCounter<long>($"{name}.other");
        using var collector = new MetricCollector(name);

        counter.Add(3, new KeyValuePair<string, object?>("outcome", "ok"));
        other.Add(9);

        var observation = collector.Snapshot().ShouldHaveSingleItem();
        observation.InstrumentName.ShouldBe(name);
        observation.LongValue.ShouldBe(3);
        observation.DoubleValue.ShouldBeNull();
        observation.Tags["outcome"].ShouldBe("ok");
    }

    [Fact]
    public void Snapshot_WhenAHistogramEmitsDoubles_CapturesTheDoubleValue()
    {
        var name = $"test.collector.{Guid.NewGuid():N}.duration";
        using var meter = new Meter(AgentKitDiagnostics.MeterName);
        var histogram = meter.CreateHistogram<double>(name);
        using var collector = new MetricCollector(name);

        histogram.Record(1.5);

        var observation = collector.Snapshot().ShouldHaveSingleItem();
        observation.DoubleValue.ShouldBe(1.5);
        observation.LongValue.ShouldBeNull();
    }

    [Fact]
    public void Snapshot_WhenAnotherMeterEmitsTheSameName_IgnoresIt()
    {
        var name = $"test.collector.{Guid.NewGuid():N}.count";
        using var foreign = new Meter("not-agentkit");
        var counter = foreign.CreateCounter<long>(name);
        using var collector = new MetricCollector(name);

        counter.Add(1);

        collector.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void Snapshot_WhenACustomMeterNameIsRequested_CapturesFromThatMeter()
    {
        var name = $"test.collector.{Guid.NewGuid():N}.count";
        var meterName = $"custom-{Guid.NewGuid():N}";
        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<long>(name);
        using var collector = new MetricCollector(name, meterName);

        counter.Add(2);

        collector.Snapshot().ShouldHaveSingleItem().LongValue.ShouldBe(2);
    }

    [Fact]
    public void Snapshot_WhenDisposed_StopsCapturing()
    {
        var name = $"test.collector.{Guid.NewGuid():N}.count";
        using var meter = new Meter(AgentKitDiagnostics.MeterName);
        var counter = meter.CreateCounter<long>(name);
        var collector = new MetricCollector(name);
        counter.Add(1);

        collector.Dispose();
        counter.Add(1);

        _ = collector.Snapshot().ShouldHaveSingleItem();
    }
}
