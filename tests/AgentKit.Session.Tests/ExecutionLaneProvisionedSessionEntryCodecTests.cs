// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies ExecutionLaneProvisionedSessionEntryCodec behavior and contracts.</summary>
public sealed class ExecutionLaneProvisionedSessionEntryCodecTests: SessionEntryCodecConformanceTests<ExecutionLaneProvisionedSessionEntryCodecTests.Fixture>
{
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance);
        public SessionEntry CreateEntry() => PortableSessionEntryCodecTestEntries.Lane();
        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) => expected == actual;
    }

    private static readonly SchemaVersion Version = new("1");
    [Fact]
    public void Constructor_WhenLimitsDiffer_CapturesIndependentDescriptorAndRuntimePolicy()
    {
        var baseline = LaneCodec();
        var wire = baseline.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var exactLimits = new SessionEntryCodecLimits(wire.Payload.Length, 2, 100, 16);
        var smallerLimits = new SessionEntryCodecLimits(wire.Payload.Length - 1, 1, 100, 16);
        var exact = new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance, exactLimits);
        var smaller = new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance, smallerLimits);
        exact.Descriptor.Limits.ShouldBeSameAs(exactLimits);
        smaller.Descriptor.Limits.ShouldBeSameAs(smallerLimits);
        _ = exact.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        _ = smaller.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenConfiguredDepthCrossesSchemaBoundary_EncodeAndDecodeAgree()
    {
        var shallow = new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance, Limits(maximumJsonDepth: 1));
        var sufficient = new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance, Limits(maximumJsonDepth: 2));
        _ = shallow.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncodeRejected>();
        var wire = sufficient.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
        _ = sufficient.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenEncodingKnownEntry_WritesStableGoldenWire()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).ShouldBe(/*lang=json,strict*/
        "{\"id\":\"00000000-0000-0000-0000-000000000001\",\"address\":{\"agentId\":\"00000000-0000-0000-0000-000000000002\",\"sessionId\":\"00000000-0000-0000-0000-000000000003\"},\"correlation\":{\"kind\":\"beforeRun\",\"operationId\":\"00000000-0000-0000-0000-000000000004\",\"admissionId\":null},\"branchId\":\"00000000-0000-0000-0000-000000000005\",\"sequence\":1,\"causalParentId\":null,\"recordedAt\":\"1970-01-01T00:00:00.0000000\\u002B00:00\",\"executionLaneId\":\"00000000-0000-0000-0000-000000000006\",\"laneRevision\":1,\"sessionProfile\":{\"key\":\"default\",\"version\":1},\"configuration\":{\"configurationVersion\":1,\"policyVersion\":1,\"fingerprint\":\"sha256:test\"}}");
        encoded.Wire.SchemaVersion.ShouldBe(Version);
    }

    [Fact]
    public void ExecutionLaneCodec_WhenUnknownNestedFieldIsWithinBounds_DecodesAndRetainsOriginalWire()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).Replace("\"sessionId\":", "\"future\":{\"nested\":[1,2]},\"sessionId\":", StringComparison.Ordinal);
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(json)]);
        var decoded = codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;
        decoded.Entry.ShouldBeOfType<ExecutionLaneProvisionedSessionEntry>().ExecutionLaneId.ShouldBe(LaneEntry().ExecutionLaneId);
        decoded.Wire.ShouldBeSameAs(wire);
    }

    [Fact]
    public void ExecutionLaneCodec_WhenUnknownNestedObjectHasDuplicateProperty_RejectsBeforeInterpretation()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).Replace("\"sessionId\":", "\"future\":{\"x\":1,\"x\":2},\"sessionId\":", StringComparison.Ordinal);
        _ = codec.Decode(Wire(codec, json)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Theory]
    [InlineData( /*lang=json,strict*/"{\"id\":\"00000000-0000-0000-0000-000000000001\",\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData( /*lang=json,strict*/"{\"id\":\"00000000-0000-0000-0000-000000000001\"}")]
    [InlineData( /*lang=json,strict*/"{\"id\":\"00000000-0000-0000-0000-00000000000A\"}")]
    [InlineData( /*lang=json,strict*/"{\"sequence\":9223372036854775808}")]
    [InlineData("[]")]
    public void ExecutionLaneCodec_WhenPayloadIsMalformed_ReturnsTypedRejection(string json)
    {
        var codec = LaneCodec();
        var wire = new SessionEntryWireEnvelope(codec.Descriptor.TypeId, Version, [.. Encoding.UTF8.GetBytes(json)]);
        _ = codec.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenDepthOrUnknownCountExceedsLimit_Rejects()
    {
        var codec = LaneCodec();
        var deep = "{\"x\":" + string.Concat(Enumerable.Repeat("{\"x\":", 17)) + "0" + new string('}', 18);
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var valid = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var many = valid[..^1] + "," + string.Join(',', Enumerable.Range(0, 65).Select(index => $"\"x{index}\":0")) + "}";
        _ = codec.Decode(Wire(codec, deep)).ShouldBeOfType<SessionEntryDecodeRejected>();
        _ = codec.Decode(Wire(codec, many)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenUnknownCountIsAtLimit_Decodes()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var valid = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var boundary = valid[..^1] + "," + string.Join(',', Enumerable.Range(0, 64).Select(index => $"\"x{index}\":0")) + "}";
        _ = codec.Decode(Wire(codec, boundary)).ShouldBeOfType<SessionEntryDecoded>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenNestedUnknownCountCrossesLimit_CountsEveryRetainedProperty()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var valid = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var atLimit = AddUnknownObject(valid, 63);
        var overLimit = AddUnknownObject(valid, 64);
        _ = codec.Decode(Wire(codec, atLimit)).ShouldBeOfType<SessionEntryDecoded>();
        _ = codec.Decode(Wire(codec, overLimit)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenUnknownBytesCrossLimit_UsesExactRawSubtreeBytes()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var valid = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var atLimit = valid[..^1] + ",\"x\":\"" + new string('a', 65_530) + "\"}";
        var overLimit = valid[..^1] + ",\"x\":\"" + new string('a', 65_531) + "\"}";
        _ = codec.Decode(Wire(codec, atLimit)).ShouldBeOfType<SessionEntryDecoded>();
        _ = codec.Decode(Wire(codec, overLimit)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenEscapedUnknownNameRetainsTooManyRawBytes_Rejects()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var valid = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var escapedName = string.Concat(Enumerable.Repeat("\\u0061", 11_000));
        var json = valid[..^1] + ",\"" + escapedName + "\":0}";
        _ = codec.Decode(Wire(codec, json)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenEncodingWouldExceedPayloadLimit_RejectsWithoutGrowingPastLimit()
    {
        var entry = LaneEntry(configurationFingerprint: new string('a', 1_100_000));
        _ = LaneCodec().Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenPayloadIsAtExactLimit_AcceptsOnlyActualBytesWithinLimit()
    {
        var codec = LaneCodec();
        var baseline = codec.Encode(LaneEntry(configurationFingerprint: "a")).ShouldBeOfType<SessionEntryEncoded>().Wire.Payload.Length;
        var exactTextLength = codec.Descriptor.Limits.MaximumPayloadBytes - baseline + 1;
        var below = codec.Encode(LaneEntry(configurationFingerprint: new string('a', exactTextLength - 1))).ShouldBeOfType<SessionEntryEncoded>();
        var exact = codec.Encode(LaneEntry(configurationFingerprint: new string('a', exactTextLength))).ShouldBeOfType<SessionEntryEncoded>();
        var above = codec.Encode(LaneEntry(configurationFingerprint: new string('a', exactTextLength + 1)));
        below.Wire.Payload.Length.ShouldBe(codec.Descriptor.Limits.MaximumPayloadBytes - 1);
        exact.Wire.Payload.Length.ShouldBe(codec.Descriptor.Limits.MaximumPayloadBytes);
        _ = above.ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenTextContainsUnpairedSurrogate_RejectsWithoutReplacement()
    {
        var entry = LaneEntry(profileKey: "profile\uD800");
        _ = LaneCodec().Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Theory]
    [InlineData("\"sessionProfile\":{\"key\":\"default\"", "\"sessionProfile\":{\"key\":\"\\uD800\"")]
    [InlineData("\"sessionId\":", "\"future\":\"\\uD800\",\"sessionId\":")]
    [InlineData("\"sessionId\":", "\"\\uD800\":0,\"sessionId\":")]
    public void ExecutionLaneCodec_WhenJsonContainsLoneSurrogateEscape_Rejects(string original, string replacement)
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var malformed = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).Replace(original, replacement, StringComparison.Ordinal);
        _ = codec.Decode(Wire(codec, malformed)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void ExecutionLaneCodec_WhenCompatibleFieldContainsEscapedSurrogatePair_DecodesAndRetainsWire()
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).Replace("\"sessionId\":", "\"future\":\"\\uD83D\\uDE00\",\"sessionId\":", StringComparison.Ordinal);
        var wire = Wire(codec, json);
        var decoded = codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        decoded.Decoded.Wire.ShouldBeSameAs(wire);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ExecutionLaneCodec_WhenRawUtf8IsInvalidAnywhere_Rejects(int location)
    {
        var codec = LaneCodec();
        var encoded = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var marker = location switch
        {
            0 => "\"default\"",
            1 => "\"sessionId\":",
            _ => "\"sessionId\":",
        };
        var replacement = location switch
        {
            0 => new byte[]
            {
                (byte)'\"',
                0xFF,
                (byte)'\"'
            },
            1 => [(byte) '\"', (byte) 'x', (byte) '\"', (byte) ':', (byte) '\"', 0xFF, (byte) '\"', (byte) ',', (byte) '\"', (byte) 's', (byte) 'e', (byte) 's', (byte) 's', (byte) 'i', (byte) 'o', (byte) 'n', (byte) 'I', (byte) 'd', (byte) '\"', (byte) ':'],
            _ => [(byte) '\"', 0xFF, (byte) '\"', (byte) ':', (byte) '0', (byte) ',', (byte) '\"', (byte) 's', (byte) 'e', (byte) 's', (byte) 's', (byte) 'i', (byte) 'o', (byte) 'n', (byte) 'I', (byte) 'd', (byte) '\"', (byte) ':'],
        };
        var bytes = Replace(Encoding.UTF8.GetBytes(json), Encoding.UTF8.GetBytes(marker), replacement);
        var wire = new SessionEntryWireEnvelope(codec.Descriptor.TypeId, Version, [.. bytes]);
        _ = codec.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Codecs_WhenUsedConcurrently_RemainDeterministic()
    {
        var lane = LaneCodec();
        var promotion = PromotionCodec();
        _ = Parallel.For(0, 128, index =>
        {
            _ = index;
            var laneWire = lane.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
            _ = lane.Decode(laneWire).ShouldBeOfType<SessionEntryDecoded>();
            var promotionWire = promotion.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
            _ = promotion.Decode(promotionWire).ShouldBeOfType<SessionEntryDecoded>();
        });
    }

    [Fact]
    public void Encode_WhenCopiedBaseStateIsMalformed_RejectsBeforeDereference()
    {
        var entry = LaneEntry() with
        {
            Address = null!,
            Id = default
        };
        _ = LaneCodec().Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Encode_WhenDiagnosticsThrow_PreservesResultAndAmbientParent()
    {
        using var parent = new Activity("portable.codec.test").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parent.SpanId)
                {
                    throw new InvalidOperationException("listener");
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var codec = new ExecutionLaneProvisionedSessionEntryCodec(new ThrowingTimeProvider(), new ThrowingLogger<ExecutionLaneProvisionedSessionEntryCodec>());
        _ = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public void Encode_WhenSuccessful_EmitsCorrelatedContentFreeActivityAndLog()
    {
        using var parent = new Activity("portable.codec.correlation").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem(AgentKitTagNames.SessionEntryCodecOperation), "execution-lane-provisioned.encode") && activity.ParentSpanId == parent.SpanId && activity.TraceId == parent.TraceId)
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new CollectingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var codec = new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, logger);
        _ = codec.Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        stopped.ShouldNotBeNull().Status.ShouldBe(ActivityStatusCode.Ok);
        stopped.GetTagItem(AgentKitTagNames.AgentId).ShouldBe(new AgentId(Id(2)).ToString());
        stopped.GetTagItem(AgentKitTagNames.SessionId).ShouldBe(new SessionId(Id(3)).ToString());
        stopped.GetTagItem(AgentKitTagNames.SessionEntryId).ShouldBe(new SessionEntryId(Id(1)).ToString());
        stopped.GetTagItem(AgentKitTagNames.SessionBranchId).ShouldBe(new BranchId(Id(5)).ToString());
        stopped.GetTagItem(AgentKitTagNames.OperationId).ShouldBe(new OperationId(Id(4)).ToString());
        stopped.GetTagItem(AgentKitTagNames.ExecutionLaneId).ShouldBe(new ExecutionLaneId(Id(6)).ToString());
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(6011);
        logger.Entries[0].Message.ShouldNotContain("sha256:test");
        logger.Entries[0].Message.ShouldNotContain("default");
    }

    [Fact]
    public void Encode_WhenRejected_EmitsErrorWithoutUnvalidatedIdentity()
    {
        using var parent = new Activity("portable.codec.rejected").Start();
        Activity? stopped = null;
        using var listener = Listener(parent, activity => stopped = activity);
        var logger = new CollectingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var codec = new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, logger);
        _ = codec.Encode(LaneEntry() with { Address = null! }).ShouldBeOfType<SessionEntryEncodeRejected>();
        stopped.ShouldNotBeNull().Status.ShouldBe(ActivityStatusCode.Error);
        stopped.GetTagItem(AgentKitTagNames.AgentId).ShouldBeNull();
        stopped.GetTagItem(AgentKitTagNames.SessionId).ShouldBeNull();
        logger.Entries.ShouldHaveSingleItem().EventId.Id.ShouldBe(6009);
    }

    [Fact]
    public void Encode_WhenMeasured_UsesOnlyBoundedMetricTags()
    {
        using var parent = new Activity("portable.codec.metric").Start();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.PropagationData : ActivitySamplingResult.None,
        };
        ActivitySource.AddActivityListener(activityListener);
        var measurements = new ConcurrentBag<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName && instrument.Name == AgentKitMetricNames.SessionEntryCodecCount)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var matches = false;
            foreach (var tag in tags)
            {
                matches |= tag.Key == AgentKitTagNames.SessionEntryCodecOperation && Equals(tag.Value, "execution-lane-provisioned.encode");
            }

            var current = Activity.Current;
            if (matches && current?.TraceId == parent.TraceId && (current.SpanId == parent.SpanId || current.ParentSpanId == parent.SpanId))
            {
                measurements.Add(tags.ToArray());
            }
        });
        listener.Start();
        _ = LaneCodec().Encode(LaneEntry()).ShouldBeOfType<SessionEntryEncoded>();
        measurements.ShouldNotBeEmpty();
        foreach (var key in measurements.SelectMany(static tags => tags).Select(static tag => tag.Key))
        {
            (key == AgentKitTagNames.SessionEntryCodecOperation || key == AgentKitTagNames.Outcome).ShouldBeTrue();
        }
    }

    private static ExecutionLaneProvisionedSessionEntryCodec LaneCodec() => new(TimeProvider.System, NullLogger<ExecutionLaneProvisionedSessionEntryCodec>.Instance);
    private static InputPromotedSessionEntryCodec PromotionCodec() => new(TimeProvider.System, NullLogger<InputPromotedSessionEntryCodec>.Instance);
    private static SessionEntryWireEnvelope Wire(ExecutionLaneProvisionedSessionEntryCodec codec, string json) => new(codec.Descriptor.TypeId, Version, [.. Encoding.UTF8.GetBytes(json)]);
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static SessionEntryCodecLimits Limits(int maximumJsonDepth) => new(1_048_576, 64, 65_536, maximumJsonDepth);
    private static ActivityListener Listener(Activity parent, Action<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem(AgentKitTagNames.SessionEntryCodecOperation), "execution-lane-provisioned.encode") && activity.ParentSpanId == parent.SpanId && activity.TraceId == parent.TraceId)
                {
                    stopped(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static byte[] Replace(byte[] source, byte[] marker, byte[] replacement)
    {
        var index = source.AsSpan().IndexOf(marker);
        index.ShouldBeGreaterThanOrEqualTo(0);
        var result = new byte[source.Length - marker.Length + replacement.Length];
        source.AsSpan(0, index).CopyTo(result);
        replacement.CopyTo(result.AsSpan(index));
        source.AsSpan(index + marker.Length).CopyTo(result.AsSpan(index + replacement.Length));
        return result;
    }

    private static string AddUnknownObject(string valid, int childCount) => valid[..^1] + ",\"future\":{" + string.Join(',', Enumerable.Range(0, childCount).Select(index => $"\"x{index}\":0")) + "}}";
    private static ExecutionLaneProvisionedSessionEntry LaneEntry(string profileKey = "default", string configurationFingerprint = "sha256:test") => new(new SessionEntryId(Id(1)), new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3))), new BeforeRunOperationCorrelation(new OperationId(Id(4)), null), new BranchId(Id(5)), new SessionSequence(1), null, DateTimeOffset.UnixEpoch, Version, new ExecutionLaneId(Id(6)), new SessionLaneRevision(1), new SessionProfileReference(new SessionProfileKey(profileKey), new SessionProfileVersion(1)), new RunConfigurationReference(new ConfigurationVersion(1), new RunPolicyVersion(1), new ContentHash(configurationFingerprint)));
    private static InputPromotedSessionEntry PromotionEntry() => new(new SessionEntryId(Id(11)), new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3))), new InRunOperationCorrelation(new OperationId(Id(4)), new RunId(Id(7)), new TurnId(Id(8))), new BranchId(Id(5)), new SessionSequence(2), new SessionEntryId(Id(1)), DateTimeOffset.UnixEpoch, Version, new ExecutionLaneId(Id(6)), new AdmissionId(Id(9)), new SessionSequence(1), [new AdmissionId(Id(9)), new AdmissionId(Id(10))]);
    private sealed class ThrowingTimeProvider: TimeProvider
    {
        public override long GetTimestamp() => throw new InvalidOperationException("clock");
    }

    private sealed class ThrowingLogger<T>: ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("logger");
    }

    private sealed class CollectingLogger<T>: ILogger<T>
    {
        public List<(EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Entries.Add((eventId, formatter(state, exception)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExecutionLaneCodecConstructor_WhenTimeProviderIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits ? Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(null!, new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>(), LimitsPortableSessionEntryCodecBoundary())) : Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(null!, new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>()));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExecutionLaneCodecConstructor_WhenLoggerIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits ? Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, null!, LimitsPortableSessionEntryCodecBoundary())) : Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, null!));
        exception.ParamName.ShouldBe("logger");
    }

    [Fact]
    public void ExecutionLaneCodecConstructor_WhenLimitsIsNull_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ExecutionLaneProvisionedSessionEntryCodec(TimeProvider.System, new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>(), null!));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void ExecutionLaneCodec_WhenInputIsNull_ThrowsBeforeObservation()
    {
        var clock = new CountingTimeProvider();
        var logger = new CountingLogger<ExecutionLaneProvisionedSessionEntryCodec>();
        var codec = new ExecutionLaneProvisionedSessionEntryCodec(clock, logger);
        AssertNoActivity(() =>
        {
            Should.Throw<ArgumentNullException>(() => codec.Encode(null!)).ParamName.ShouldBe("entry");
            Should.Throw<ArgumentNullException>(() => codec.Decode(null!)).ParamName.ShouldBe("wire");
        });
        clock.TimestampCalls.ShouldBe(0);
        logger.IsEnabledCalls.ShouldBe(0);
        logger.LogCalls.ShouldBe(0);
    }

    private static SessionEntryCodecLimits LimitsPortableSessionEntryCodecBoundary() => new(1024, 1, 128, 4);
    private static void AssertNoActivity(Action action)
    {
        using var parent = new Activity("portable.codec.boundary").SetIdFormat(ActivityIdFormat.W3C).Start();
        var parentTraceId = parent.TraceId;
        var parentSpanId = parent.SpanId;
        var started = false;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.PropagationData : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.SessionEntryCodec && activity.ParentSpanId == parentSpanId && activity.TraceId == parentTraceId)
                {
                    started = true;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        action();
        started.ShouldBeFalse();
    }

    private sealed class CountingTimeProvider: TimeProvider
    {
        public int TimestampCalls { get; private set; }

        public override long GetTimestamp()
        {
            TimestampCalls++;
            return 0;
        }
    }

    private sealed class CountingLogger<T>: ILogger<T>
    {
        public int IsEnabledCalls { get; private set; }
        public int LogCalls { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel)
        {
            IsEnabledCalls++;
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => LogCalls++;
    }
}
