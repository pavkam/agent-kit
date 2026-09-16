// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Diagnostics;
using System.Text;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies InputPromotedSessionEntryCodec behavior and contracts.</summary>
public sealed class InputPromotedSessionEntryCodecTests: SessionEntryCodecConformanceTests<InputPromotedSessionEntryCodecTests.Fixture>
{
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new InputPromotedSessionEntryCodec(TimeProvider.System, NullLogger<InputPromotedSessionEntryCodec>.Instance);
        public SessionEntry CreateEntry() => PortableSessionEntryCodecTestEntries.Promotion();
        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual)
        {
            var left = (InputPromotedSessionEntry) expected;
            var right = (InputPromotedSessionEntry) actual;
            return left.Id == right.Id && left.Address == right.Address && left.Correlation == right.Correlation && left.BranchId == right.BranchId && left.Sequence == right.Sequence && left.CausalParentId == right.CausalParentId && left.RecordedAt == right.RecordedAt && left.SchemaVersion == right.SchemaVersion && left.ExecutionLaneId == right.ExecutionLaneId && left.InitiatingAdmissionId == right.InitiatingAdmissionId && left.Cutoff == right.Cutoff && left.AdmissionIds.SequenceEqual(right.AdmissionIds);
        }
    }

    private static readonly SchemaVersion Version = new("1");
    [Fact]
    public void InputPromotedCodec_WhenConfiguredDepthCrossesSchemaBoundary_EncodeAndDecodeAgree()
    {
        var shallow = new InputPromotedSessionEntryCodec(TimeProvider.System, NullLogger<InputPromotedSessionEntryCodec>.Instance, Limits(maximumJsonDepth: 1));
        var sufficient = new InputPromotedSessionEntryCodec(TimeProvider.System, NullLogger<InputPromotedSessionEntryCodec>.Instance, Limits(maximumJsonDepth: 2));
        _ = shallow.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncodeRejected>();
        var wire = sufficient.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>().Wire;
        _ = sufficient.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
    }

    [Fact]
    public void Constructor_WhenLimitsIsNull_ThrowsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(TimeProvider.System, NullLogger<InputPromotedSessionEntryCodec>.Instance, null!));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void InputPromotedCodec_WhenEncodingKnownEntry_PreservesAdmissionOrder()
    {
        var codec = PromotionCodec();
        var expected = PromotionEntry();
        var encoded = codec.Encode(expected).ShouldBeOfType<SessionEntryEncoded>();
        var decoded = codec.Decode(encoded.Wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;
        var actual = decoded.Entry.ShouldBeOfType<InputPromotedSessionEntry>();
        actual.Id.ShouldBe(expected.Id);
        actual.Address.ShouldBe(expected.Address);
        actual.Correlation.ShouldBe(expected.Correlation);
        actual.AdmissionIds.ShouldBe(expected.AdmissionIds);
        actual.InitiatingAdmissionId.ShouldBe(expected.InitiatingAdmissionId);
        decoded.Wire.ShouldBeSameAs(encoded.Wire);
    }

    [Fact]
    public void InputPromotedCodec_WhenEncodingKnownEntry_WritesStableGoldenWire()
    {
        var encoded = PromotionCodec().Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).ShouldBe(/*lang=json,strict*/
        "{\"id\":\"00000000-0000-0000-0000-000000000011\",\"address\":{\"agentId\":\"00000000-0000-0000-0000-000000000002\",\"sessionId\":\"00000000-0000-0000-0000-000000000003\"},\"correlation\":{\"kind\":\"inRun\",\"operationId\":\"00000000-0000-0000-0000-000000000004\",\"runId\":\"00000000-0000-0000-0000-000000000007\",\"turnId\":\"00000000-0000-0000-0000-000000000008\"},\"branchId\":\"00000000-0000-0000-0000-000000000005\",\"sequence\":2,\"causalParentId\":\"00000000-0000-0000-0000-000000000001\",\"recordedAt\":\"1970-01-01T00:00:00.0000000\\u002B00:00\",\"executionLaneId\":\"00000000-0000-0000-0000-000000000006\",\"initiatingAdmissionId\":\"00000000-0000-0000-0000-000000000009\",\"cutoff\":1,\"admissionIds\":[\"00000000-0000-0000-0000-000000000009\",\"00000000-0000-0000-0000-000000000010\"]}");
    }

    [Fact]
    public void InputPromotedCodec_WhenAdmissionArrayCannotFitPayload_RejectsBeforeDistinctEncodingWork()
    {
        var codec = PromotionCodec();
        var count = (codec.Descriptor.Limits.MaximumPayloadBytes / 39) + 1;
        var admissions = Enumerable.Range(1, count).Select(index => new AdmissionId(Id(index))).ToImmutableArray();
        var template = PromotionEntry();
        var entry = new InputPromotedSessionEntry(template.Id, template.Address, (InRunOperationCorrelation) template.Correlation, template.BranchId, template.Sequence, template.CausalParentId, template.RecordedAt, template.SchemaVersion, template.ExecutionLaneId, admissions[0], template.Cutoff, admissions);
        _ = codec.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Decode_WhenPayloadIsNotWellFormedJson_ReturnsRejectedWithParserReason()
    {
        var codec = PromotionCodec();
        var wire = new SessionEntryWireEnvelope(codec.Descriptor.TypeId, Version, [.. "{not json"u8]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason
            .ShouldBe("The session-entry payload is not a bounded JSON object.");
    }

    [Fact]
    public void Decode_WhenSequenceIsNotPositive_ReturnsMalformedRejection()
    {
        var codec = PromotionCodec();
        var encoded = codec.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = System.Text.Json.Nodes.JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        json["sequence"] = 0;
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion,
            [.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason
            .ShouldBe("The input-promotion payload is malformed.");
    }

    [Fact]
    public void Decode_WhenAdmissionIdsFieldIsMissing_ReturnsMalformedRejection()
    {
        var codec = PromotionCodec();
        var encoded = codec.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = System.Text.Json.Nodes.JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        _ = json.Remove("admissionIds");
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion,
            [.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason
            .ShouldBe("The input-promotion payload is malformed.");
    }

    [Fact]
    public void Decode_WhenAdmissionIdsIsEmptyArray_ReturnsMalformedRejection()
    {
        var codec = PromotionCodec();
        var encoded = codec.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = System.Text.Json.Nodes.JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        json["admissionIds"] = new System.Text.Json.Nodes.JsonArray();
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion,
            [.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason
            .ShouldBe("The input-promotion payload is malformed.");
    }

    [Fact]
    public void Decode_WhenAdmissionIdEntryIsMalformed_ReturnsMalformedRejection()
    {
        var codec = PromotionCodec();
        var encoded = codec.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = System.Text.Json.Nodes.JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        json["admissionIds"]![0] = "not-a-guid";
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion,
            [.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason
            .ShouldBe("The input-promotion payload is malformed.");
    }

    [Fact]
    public void Decode_WhenInitiatingAdmissionIsNotAmongAdmissionIds_ReturnsSemanticRejection()
    {
        var codec = PromotionCodec();
        var encoded = codec.Encode(PromotionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = System.Text.Json.Nodes.JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        json["initiatingAdmissionId"] = Id(999).ToString();
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion,
            [.. System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason
            .ShouldBe("The input-promotion payload violates semantic constraints.");
    }

    private static InputPromotedSessionEntryCodec PromotionCodec() => new(TimeProvider.System, NullLogger<InputPromotedSessionEntryCodec>.Instance);
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
    private static SessionEntryCodecLimits Limits(int maximumJsonDepth) => new(1_048_576, 64, 65_536, maximumJsonDepth);
    private static InputPromotedSessionEntry PromotionEntry() => new(new SessionEntryId(Id(11)), new SessionAddress(new AgentId(Id(2)), new SessionId(Id(3))), new InRunOperationCorrelation(new OperationId(Id(4)), new RunId(Id(7)), new TurnId(Id(8))), new BranchId(Id(5)), new SessionSequence(2), new SessionEntryId(Id(1)), DateTimeOffset.UnixEpoch, Version, new ExecutionLaneId(Id(6)), new AdmissionId(Id(9)), new SessionSequence(1), [new AdmissionId(Id(9)), new AdmissionId(Id(10))]);
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InputPromotedCodecConstructor_WhenTimeProviderIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits ? Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(null!, new CountingLogger<InputPromotedSessionEntryCodec>(), LimitsPortableSessionEntryCodecBoundary())) : Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(null!, new CountingLogger<InputPromotedSessionEntryCodec>()));
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InputPromotedCodecConstructor_WhenLoggerIsNull_ThrowsExactParameterName(bool withLimits)
    {
        var exception = withLimits ? Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(TimeProvider.System, null!, LimitsPortableSessionEntryCodecBoundary())) : Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(TimeProvider.System, null!));
        exception.ParamName.ShouldBe("logger");
    }

    [Fact]
    public void InputPromotedCodecConstructor_WhenLimitsIsNull_ThrowsExactParameterName()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InputPromotedSessionEntryCodec(TimeProvider.System, new CountingLogger<InputPromotedSessionEntryCodec>(), null!));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void InputPromotedCodec_WhenInputIsNull_ThrowsBeforeObservation()
    {
        var clock = new CountingTimeProvider();
        var logger = new CountingLogger<InputPromotedSessionEntryCodec>();
        var codec = new InputPromotedSessionEntryCodec(clock, logger);
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
