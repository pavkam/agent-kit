// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

/// <summary>Verifies SessionEntryCodecCatalog behavior and contracts.</summary>
public sealed class SessionEntryCodecCatalogTests
{
    [Fact]
    public void Constructor_WhenDescriptorIsReadTwiceByTheCatalog_WouldThrowButCapturesOnce()
    {
        var codec = new FakeCodec(throwOnSecondDescriptorRead: true);
        _ = new SessionEntryCodecCatalog([codec], TimeProvider.System);
        codec.DescriptorReads.ShouldBe(1);
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new SessionEntryCodecCatalog([], null!)).ParamName.ShouldBe("timeProvider");

    [Fact]
    public void Constructor_WhenCodecDescriptorIsNull_ThrowsArgumentException() => Should.Throw<ArgumentException>(() => new SessionEntryCodecCatalog([new FakeCodec { ReturnNullDescriptor = true }], TimeProvider.System)).ParamName.ShouldBe("codecs");

    [Fact]
    public void ThrowIfNullSessionEntryCodecDescriptor_WhenDescriptorIsNull_ThrowsWithInferredParameterName()
    {
        SessionEntryCodecDescriptor? descriptor = null;
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfNullSessionEntryCodecDescriptor(descriptor));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void ThrowIfDuplicateSessionEntryCodecBindings_WhenBindingsRepeatIdentity_ThrowsWithInferredParameterName()
    {
        var first = new FakeCodec();
        var second = new FakeCodec();
        IReadOnlyList<SessionEntryCodecBinding> bindings = [new SessionEntryCodecBinding(first, first.Descriptor), new SessionEntryCodecBinding(second, second.Descriptor),];
        var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIfDuplicateSessionEntryCodecBindings(bindings));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("bindings");
    }

    [Fact]
    public void Decode_WhenWireIdentityOrSchemaIsUnknown_ReturnsOpaqueWithoutInvokingCodec()
    {
        var codec = new FakeCodec();
        var catalog = new SessionEntryCodecCatalog([codec], TimeProvider.System);
        _ = catalog.Decode(new SessionEntryWireEnvelope(new SessionEntryTypeId("other"), Version, [1])).ShouldBeOfType<SessionEntryOpaque>();
        _ = catalog.Decode(new SessionEntryWireEnvelope(Type, new SchemaVersion("other"), [1])).ShouldBeOfType<SessionEntryOpaque>();
        codec.DecodeCalls.ShouldBe(0);
    }

    [Fact]
    public void EncodeAndDecode_WhenCodecOwnsExactEntryAndWire_ReturnsCodecResults()
    {
        var codec = new FakeCodec();
        var catalog = new SessionEntryCodecCatalog([codec], TimeProvider.System);
        var entry = new TestEntry();
        _ = catalog.Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        _ = catalog.Decode(codec.Wire).ShouldBeOfType<SessionEntryDecoded>();
        codec.EncodeCalls.ShouldBe(1);
        codec.DecodeCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Encode_WhenCodecReturnsInvalidEnvelope_Rejects(bool wrongType, bool wrongVersion, bool oversized)
    {
        var codec = new FakeCodec
        {
            EncodeWire = new SessionEntryWireEnvelope(wrongType ? new SessionEntryTypeId("wrong") : Type, wrongVersion ? new SchemaVersion("wrong") : Version, oversized ? [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17] : [1])
        };
        _ = new SessionEntryCodecCatalog([codec], TimeProvider.System).Encode(new TestEntry()).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Decode_WhenPayloadExceedsCapturedLimit_RejectsWithoutInvokingCodec()
    {
        var codec = new FakeCodec();
        var result = new SessionEntryCodecCatalog([codec], TimeProvider.System).Decode(new SessionEntryWireEnvelope(Type, Version, [.. Enumerable.Repeat((byte) 1, 17)]));
        _ = result.ShouldBeOfType<SessionEntryDecodeRejected>();
        codec.DecodeCalls.ShouldBe(0);
    }

    [Fact]
    public void Decode_WhenUnknownPayloadExceedsGlobalLimit_RejectsWithoutInvokingCodec()
    {
        var codec = new FakeCodec();
        var catalog = new SessionEntryCodecCatalog([codec], TimeProvider.System, new SessionEntryCodecCatalogOptions(1));
        _ = catalog.Decode(new SessionEntryWireEnvelope(new SessionEntryTypeId("unknown"), Version, [1, 2])).ShouldBeOfType<SessionEntryDecodeRejected>();
        codec.DecodeCalls.ShouldBe(0);
    }

    [Fact]
    public void Decode_WhenUnknownPayloadIsAtGlobalLimit_ReturnsOpaque() => _ = new SessionEntryCodecCatalog([], TimeProvider.System, new SessionEntryCodecCatalogOptions(1)).Decode(new SessionEntryWireEnvelope(new SessionEntryTypeId("unknown"), Version, [255])).ShouldBeOfType<SessionEntryOpaque>();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Options_WhenMaximumPayloadIsNotPositive_ThrowsArgumentOutOfRangeException(int maximumPayloadBytes) => Should.Throw<ArgumentOutOfRangeException>(() => new SessionEntryCodecCatalogOptions(maximumPayloadBytes)).ParamName.ShouldBe("maximumPayloadBytes");

    [Fact]
    public void Options_WhenMaximumPayloadIsPositive_PreservesValue() => new SessionEntryCodecCatalogOptions(int.MaxValue).MaximumPayloadBytes.ShouldBe(int.MaxValue);

    [Fact]
    public void Options_WhenMaximumPayloadMatches_AreEqual()
    {
        var first = new SessionEntryCodecCatalogOptions(16);
        var second = new SessionEntryCodecCatalogOptions(16);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Options_WhenCloned_ProducesAnEquivalentInstance()
    {
        var original = new SessionEntryCodecCatalogOptions(16);

        var clone = original with { };

        clone.ShouldBe(original);
    }
    [Fact]
    public void Constructor_WhenOptionsIsNull_UsesDocumentedDefault() => _ = new SessionEntryCodecCatalog([], TimeProvider.System, null).Decode(new SessionEntryWireEnvelope(new SessionEntryTypeId("unknown"), Version, [1])).ShouldBeOfType<SessionEntryOpaque>();

    [Fact]
    public void Encode_WhenCodecLegitimatelyRejects_ReturnsThatExactResult()
    {
        var codec = new FakeCodec { RejectEncode = true };
        var result = new SessionEntryCodecCatalog([codec], TimeProvider.System).Encode(new TestEntry());
        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe("codec rejected");
    }

    [Fact]
    public void Decode_WhenCodecLegitimatelyRejects_ReturnsThatExactResult()
    {
        var codec = new FakeCodec { RejectDecode = true };
        var catalog = new SessionEntryCodecCatalog([codec], TimeProvider.System);
        var result = catalog.Decode(codec.Wire);
        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe("codec rejected");
    }

    [Fact]
    public void Decode_WhenCodecReplacesInputWire_Rejects()
    {
        var codec = new FakeCodec
        {
            ReplaceDecodeWire = true
        };
        _ = new SessionEntryCodecCatalog([codec], TimeProvider.System).Decode(codec.Wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void EncodeOrDecode_WhenCodecReturnsNull_Rejects()
    {
        _ = new SessionEntryCodecCatalog([new FakeCodec { ReturnNullEncode = true }], TimeProvider.System).Encode(new TestEntry()).ShouldBeOfType<SessionEntryEncodeRejected>();
        var codec = new FakeCodec
        {
            ReturnNullDecode = true
        };
        _ = new SessionEntryCodecCatalog([codec], TimeProvider.System).Decode(codec.Wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Constructor_WhenDistinctCodecsReuseTypeOrWire_ThrowsArgumentException() => Should.Throw<ArgumentException>(() => new SessionEntryCodecCatalog([new FakeCodec(), new FakeCodec()], TimeProvider.System)).ParamName.ShouldBe("codecs");
    [Fact]
    public void Encode_WhenObserved_EmitsSafeCorrelatedDiagnostics()
    {
        using var parent = new Activity("parent").SetIdFormat(ActivityIdFormat.W3C).Start();
        var parentSpanId = parent.SpanId;
        Activity? completed = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parent.SpanId)
                {
                    completed = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        ConcurrentQueue<(string Name, double Value, IReadOnlyDictionary<string, object?> Tags)> measurements = [];
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SessionEntryCodecCount or AgentKitMetricNames.SessionEntryCodecDuration)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (IsCodecObservationUnder(parentSpanId))
            {
                measurements.Enqueue((instrument.Name, measurement, tags.ToArray().ToDictionary()));
            }
        });
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (IsCodecObservationUnder(parentSpanId))
            {
                measurements.Enqueue((instrument.Name, measurement, tags.ToArray().ToDictionary()));
            }
        });
        meterListener.Start();
        var logger = new RecordingLogger();
        _ = new SessionEntryCodecCatalog([new FakeCodec()], TimeProvider.System, logger: logger).Encode(new TestEntry());
        _ = completed.ShouldNotBeNull();
        completed.ParentSpanId.ShouldBe(parent.SpanId);
        completed.Status.ShouldBe(ActivityStatusCode.Ok);
        completed.GetTagItem(AgentKitTagNames.SessionEntryCodecOperation).ShouldBe("encode");
        completed.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("encoded");
        measurements.ShouldContain(measurement => measurement.Name == AgentKitMetricNames.SessionEntryCodecCount && (string?) measurement.Tags[AgentKitTagNames.SessionEntryCodecOperation] == "encode" && (string?) measurement.Tags[AgentKitTagNames.Outcome] == "encoded");
        measurements.ShouldContain(measurement => measurement.Name == AgentKitMetricNames.SessionEntryCodecDuration && measurement.Value >= 0);
        var log = logger.Entries.ShouldHaveSingleItem();
        log.EventId.Id.ShouldBe(6009);
        log.Properties.Count.ShouldBe(3);
        log.Properties.ShouldContainKey("{OriginalFormat}");
        log.Properties["Operation"].ShouldBe("encode");
        log.Properties["Outcome"].ShouldBe("encoded");
    }

    [Fact]
    public void Encode_WhenArgumentIsNull_ThrowsBeforeObservation()
    {
        using var parent = new Activity("parent").Start();
        var logger = new RecordingLogger();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parent.SpanId)
                {
                    throw new Xunit.Sdk.XunitException("An activity must not start.");
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        var catalog = new SessionEntryCodecCatalog([], TimeProvider.System, logger: logger);
        Should.Throw<ArgumentNullException>(() => catalog.Encode(null!)).ParamName.ShouldBe("entry");
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Encode_WhenObserversThrow_PreservesCodecResult()
    {
        using var parent = new Activity("parent").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parent.SpanId)
                {
                    throw new InvalidOperationException("activity");
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        using var meterListener = CreateThrowingMeterListener(parent.TraceId);
        _ = new SessionEntryCodecCatalog([new FakeCodec()], new ThrowingTimeProvider(), logger: new ThrowingLogger()).Encode(new TestEntry()).ShouldBeOfType<SessionEntryEncoded>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Encode_WhenCodecThrowsAndObserversThrow_PreservesOriginalException()
    {
        using var parent = new Activity("parent").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parent.SpanId)
                {
                    throw new InvalidOperationException("activity stop");
                }
            },
        };
        ActivitySource.AddActivityListener(activityListener);
        var expected = new CodecException();
        using var meterListener = CreateThrowingMeterListener(parent.TraceId);
        var codec = new FakeCodec
        {
            EncodeException = expected
        };
        var catalog = new SessionEntryCodecCatalog([codec], new ThrowingTimeProvider(), logger: new ThrowingLogger());
        Should.Throw<CodecException>(() => catalog.Encode(new TestEntry())).ShouldBeSameAs(expected);
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Encode_WhenTimestampClockThrows_EmitsCountAndOmitsDuration()
    {
        using var parent = new Activity("session.entry.codec.clock").SetIdFormat(ActivityIdFormat.W3C).Start();
        var parentSpanId = parent.SpanId;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.PropagationData : ActivitySamplingResult.None,
        };
        ActivitySource.AddActivityListener(activityListener);
        ConcurrentQueue<string> instrumentNames = [];
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SessionEntryCodecCount or AgentKitMetricNames.SessionEntryCodecDuration)
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
        {
            if (IsCodecObservationUnder(parentSpanId))
            {
                instrumentNames.Enqueue(instrument.Name);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) =>
        {
            if (IsCodecObservationUnder(parentSpanId))
            {
                instrumentNames.Enqueue(instrument.Name);
            }
        });
        listener.Start();
        _ = new SessionEntryCodecCatalog([new FakeCodec()], new ThrowingTimeProvider()).Encode(new TestEntry());
        instrumentNames.ShouldContain(AgentKitMetricNames.SessionEntryCodecCount);
        instrumentNames.ShouldNotContain(AgentKitMetricNames.SessionEntryCodecDuration);
    }

    [Fact]
    public void Encode_WhenCodecThrows_ActivityReportsFaultWithoutReplacingException()
    {
        using var parent = new Activity("parent").Start();
        Activity? completed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parent.SpanId)
                {
                    completed = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var expected = new CodecException();
        var logger = new RecordingLogger();
        var catalog = new SessionEntryCodecCatalog([new FakeCodec { EncodeException = expected }], TimeProvider.System, logger: logger);
        Should.Throw<CodecException>(() => catalog.Encode(new TestEntry())).ShouldBeSameAs(expected);
        _ = completed.ShouldNotBeNull();
        completed.Status.ShouldBe(ActivityStatusCode.Error);
        completed.StatusDescription.ShouldBe(nameof(CodecException));
        completed.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("faulted");
        completed.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(nameof(CodecException));
        var log = logger.Entries.ShouldHaveSingleItem();
        log.EventId.Id.ShouldBe(6010);
        log.Properties.Count.ShouldBe(3);
        log.Properties["Operation"].ShouldBe("encode");
        log.Properties["ErrorType"].ShouldBe(nameof(CodecException));
    }

    [Fact]
    public void Encode_WhenRejected_ActivityReportsErrorWithBoundedOutcome()
    {
        using var parent = new Activity("parent").Start();
        Activity? completed = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parent.SpanId)
                {
                    completed = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        _ = new SessionEntryCodecCatalog([], TimeProvider.System).Encode(new TestEntry()).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = completed.ShouldNotBeNull();
        completed.Status.ShouldBe(ActivityStatusCode.Error);
        completed.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("rejected");
    }

    /// <summary>Creates an owned fault-injection listener confined to one test's codec operations.</summary>
    /// <param name = "traceId">The trace identity of the test-owned parent activity.</param>
    /// <returns>A started listener that throws only for measurements within the supplied trace; the caller disposes it.</returns>
    private static MeterListener CreateThrowingMeterListener(ActivityTraceId traceId)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name is AgentKitMetricNames.SessionEntryCodecCount or AgentKitMetricNames.SessionEntryCodecDuration)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
        {
            if (Activity.Current?.TraceId == traceId)
            {
                throw new InvalidOperationException("meter");
            }
        });
        listener.SetMeasurementEventCallback<double>((_, _, _, _) =>
        {
            if (Activity.Current?.TraceId == traceId)
            {
                throw new InvalidOperationException("meter");
            }
        });
        listener.Start();
        return listener;
    }

    /// <summary>Returns whether a synchronous metric callback belongs to this test's codec activity.</summary>
    /// <param name = "parentSpanId">The span identity of the test-owned parent activity.</param>
    /// <returns><see langword="true"/> only for the codec activity directly parented by <paramref name = "parentSpanId"/>.</returns>
    private static bool IsCodecObservationUnder(ActivitySpanId parentSpanId) => Activity.Current is { OperationName: AgentKitActivityNames.SessionEntryCodec } activity && activity.ParentSpanId == parentSpanId;
    private static readonly SessionEntryTypeId Type = new("agentkit.test/v1");
    private static readonly SchemaVersion Version = new("agentkit.test/v1");
    private sealed class FakeCodec(bool throwOnSecondDescriptorRead = false): ISessionEntryCodec
    {
        public int DescriptorReads { get; private set; }
        public int EncodeCalls { get; private set; }
        public int DecodeCalls { get; private set; }
        public SessionEntryWireEnvelope Wire { get; } = new(Type, Version, [1]);
        public SessionEntryWireEnvelope? EncodeWire { get; init; }
        public bool ReplaceDecodeWire { get; init; }
        public bool ReturnNullEncode { get; init; }
        public bool ReturnNullDecode { get; init; }
        public bool RejectEncode { get; init; }
        public bool RejectDecode { get; init; }
        public Exception? EncodeException { get; init; }
        public bool ReturnNullDescriptor { get; init; }
        public SessionEntryCodecDescriptor Descriptor { get => ReturnNullDescriptor ? null! : ++DescriptorReads > 1 && throwOnSecondDescriptorRead ? throw new InvalidOperationException() : field; } = new(Type, typeof(TestEntry), Version, [Version], new SessionEntryCodecLimits(16, 1, 1, 2));

        public SessionEntryEncodeResult Encode(SessionEntry entry)
        {
            EncodeCalls++;
            return EncodeException is { } exception
                ? throw exception
                : RejectEncode
                    ? new SessionEntryEncodeRejected("codec rejected")
                    : ReturnNullEncode
                        ? null!
                        : new SessionEntryEncoded(EncodeWire ?? Wire);
        }

        public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire)
        {
            DecodeCalls++;
            if (RejectDecode)
            {
                return new SessionEntryDecodeRejected("codec rejected");
            }

            if (ReturnNullDecode)
            {
                return null!;
            }

            var retained = ReplaceDecodeWire ? new SessionEntryWireEnvelope(Type, Version, [2]) : wire;
            return new SessionEntryDecoded(new DecodedSessionEntry(new TestEntry(), retained));
        }
    }

    private sealed record TestEntry: SessionEntry
    {
        public TestEntry() : base(default, new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())), new BeforeRunOperationCorrelation(new OperationId(Guid.NewGuid()), null), new BranchId(Guid.NewGuid()), new SessionSequence(1), null, DateTimeOffset.UnixEpoch, Version)
        {
        }
    }

    private sealed class CodecException: Exception;
    private sealed class ThrowingTimeProvider: TimeProvider
    {
        public override long GetTimestamp() => throw new InvalidOperationException("clock");
    }

    private sealed class ThrowingLogger: ILogger<SessionEntryCodecCatalog>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger");
    }

    private sealed class RecordingLogger: ILogger<SessionEntryCodecCatalog>
    {
        public List<(EventId EventId, IReadOnlyDictionary<string, object?> Properties)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values ? values.ToDictionary(static pair => pair.Key, static pair => pair.Value) : throw new InvalidOperationException("Structured log state was expected.");
            Entries.Add((eventId, properties));
        }
    }
}
