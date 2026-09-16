// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies OperationAcceptedSessionEntryCodec behavior and contracts.</summary>
public sealed class OperationAcceptedSessionEntryCodecTests: SessionEntryCodecConformanceTests<OperationAcceptedSessionEntryCodecTests.Fixture>
{
    [Fact]
    public void EncodeDecode_WhenAcceptedStateIsComplete_PreservesEveryRecoveryFieldAndOriginalWire()
    {
        var codec = Codec();
        var expected = OperationAcceptedSessionEntryCodecTestData.Entry();
        var wire = codec.Encode(expected).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var decoded = codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;
        var actual = decoded.Entry.ShouldBeOfType<OperationAcceptedSessionEntry>();
        OperationAcceptedSessionEntryCodecTestData.Equivalent(expected, actual).ShouldBeTrue();
        actual.State.Authorization.Identity.ShouldBeSameAs(actual.State.Identity);
        actual.RecordedAt.Offset.ShouldBe(TimeSpan.FromMinutes(90));
        actual.State.AcceptedAt.Offset.ShouldBe(TimeSpan.FromMinutes(90));
        actual.State.Identity.Evidence.AuthenticatedAt.Offset.ShouldBe(TimeSpan.FromHours(2));
        actual.State.Identity.Evidence.ExpiresAt!.Value.Offset.ShouldBe(TimeSpan.FromHours(2));
        actual.State.Identity.DelegationChain[0].DelegatedAt.Offset.ShouldBe(TimeSpan.FromHours(2));
        decoded.Wire.ShouldBeSameAs(wire);
        wire.TypeId.ShouldBe(new SessionEntryTypeId("agentkit.session/operation-accepted"));
        wire.SchemaVersion.ShouldBe(new SchemaVersion("1"));
    }

    [Fact]
    public void Decode_WhenUnknownNestedEvidenceIsWithinBounds_PreservesExactWire()
    {
        var codec = Codec();
        var encoded = codec.Encode(OperationAcceptedSessionEntryCodecTestData.Entry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).Replace("\"method\":", "\"future\":{\"x\":1},\"method\":", StringComparison.Ordinal);
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(json)]);
        var decoded = codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        decoded.Decoded.Wire.ShouldBeSameAs(wire);
    }

    [Theory]
    [InlineData("\"state\":\"accepted\"", "\"state\":\"settled\"")]
    [InlineData("\"identityRef\":\"state.identity\"", "\"identityRef\":\"other\"")]
    [InlineData("\"correlationRef\":\"state.correlation\"", "\"correlationRef\":\"other\"")]
    public void Decode_WhenRecoveryEvidenceIsInconsistent_ReturnsTypedRejection(string original, string replacement)
    {
        var codec = Codec();
        var encoded = codec.Encode(OperationAcceptedSessionEntryCodecTestData.Entry()).ShouldBeOfType<SessionEntryEncoded>();
        var source = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        source.ShouldContain(original);
        var json = source.Replace(original, replacement, StringComparison.Ordinal);
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(json)]);
        _ = codec.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenAuthorizationConfigurationDiffersFromStateConfiguration_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(OperationAcceptedSessionEntryCodecTestData.Entry()).ShouldBeOfType<SessionEntryEncoded>();
        var root = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        root["state"]!["authorization"]!["configurationVersion"] = 2;
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(root.ToJsonString())]);
        _ = codec.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenOrderedRecoveryArraysViolateRelations_RejectsEachMalformedEnvelope()
    {
        var codec = Codec();
        var encoded = codec.Encode(OperationAcceptedSessionEntryCodecTestData.Entry()).ShouldBeOfType<SessionEntryEncoded>();
        var duplicateAdmission = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        var admissions = duplicateAdmission["state"]!["promotedAdmissionIds"]!.AsArray();
        admissions[1] = admissions[0]!.GetValue<string>();
        var missingMessage = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        missingMessage["state"]!["materializedMessageIds"]!.AsArray().RemoveAt(1);
        var emptyEntries = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        emptyEntries["state"]!["materializedEntryIds"] = new JsonArray();
        _ = codec.Decode(Wire(encoded.Wire, duplicateAdmission)).ShouldBeOfType<SessionEntryDecodeRejected>();
        _ = codec.Decode(Wire(encoded.Wire, missingMessage)).ShouldBeOfType<SessionEntryDecodeRejected>();
        _ = codec.Decode(Wire(encoded.Wire, emptyEntries)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenGuidArrayFieldIsMissingOrNotAnArray_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(OperationAcceptedSessionEntryCodecTestData.Entry()).ShouldBeOfType<SessionEntryEncoded>();
        var missingField = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        _ = missingField["state"]!.AsObject().Remove("materializedEntryIds");
        var notAnArray = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        notAnArray["state"]!["materializedEntryIds"] = 1;

        _ = codec.Decode(Wire(encoded.Wire, missingField)).ShouldBeOfType<SessionEntryDecodeRejected>();
        _ = codec.Decode(Wire(encoded.Wire, notAnArray)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Encode_WhenCopiedBaseIdentityIsDefault_RejectsBeforeDiagnosticsEvidence()
    {
        var malformed = OperationAcceptedSessionEntryCodecTestData.Entry() with
        {
            Id = default
        };
        _ = Codec().Encode(malformed).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Encode_WhenRetainedConfigurationTextIsMalformed_RejectsWithoutReplacement()
    {
        var malformedProfile = OperationAcceptedSessionEntryCodecTestData.Entry("profile" + '\uD800');
        var malformedConfiguration = OperationAcceptedSessionEntryCodecTestData.Entry(configurationFingerprint: "hash" + '\uD800');
        _ = Codec().Encode(malformedProfile).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = Codec().Encode(malformedConfiguration).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Encode_WhenIdentityClaimArrayCannotFitPayload_RejectsBeforeTraversingClaimText()
    {
        var codec = Codec();
        var count = (codec.Descriptor.Limits.MaximumPayloadBytes / 48) + 1;
        var entry = OperationAcceptedSessionEntryCodecTestData.Entry(identityClaimCount: count);
        _ = codec.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Encode_WhenSuccessful_EmitsValidatedAcceptedStateCorrelation()
    {
        using var parent = new Activity("operation.accepted.codec.test").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Name == AgentKitActivityNames.SessionEntryCodec && options.Parent == parent.Context ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId == parent.SpanId && activity.TraceId == parent.TraceId && Equals(activity.GetTagItem(AgentKitTagNames.SessionEntryCodecOperation), "operation-accepted.encode"))
                {
                    stopped = activity;
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var entry = OperationAcceptedSessionEntryCodecTestData.Entry();
        _ = Codec().Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        stopped.ShouldNotBeNull().Status.ShouldBe(ActivityStatusCode.Ok);
        stopped.GetTagItem(AgentKitTagNames.SessionEntryId).ShouldBe(entry.Id.ToString());
        stopped.GetTagItem(AgentKitTagNames.ExecutionLaneId).ShouldBe(entry.State.ExecutionLaneId.ToString());
        stopped.GetTagItem(AgentKitTagNames.RunId).ShouldBe(entry.State.Correlation.RunId.ToString());
        stopped.GetTagItem(AgentKitTagNames.TurnId).ShouldBe(entry.State.InitialTurnId.ToString());
    }

    [Fact]
    public void Operations_WhenLimitsAreIndependent_ApplyCapturedDepthAndPayloadPolicy()
    {
        var entry = OperationAcceptedSessionEntryCodecTestData.Entry();
        var sufficient = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(1_048_576, 64, 65_536, 24));
        var shallow = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(1_048_576, 64, 65_536, 2));
        var wire = sufficient.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        _ = sufficient.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        _ = shallow.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
        shallow.Descriptor.Limits.MaximumJsonDepth.ShouldBe(2);
    }

    private static OperationAcceptedSessionEntryCodec Codec() => new(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance);
    private static SessionEntryWireEnvelope Wire(SessionEntryWireEnvelope template, JsonObject root) => new(template.TypeId, template.SchemaVersion, [.. Encoding.UTF8.GetBytes(root.ToJsonString())]);
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance);
        public SessionEntry CreateEntry() => OperationAcceptedSessionEntryCodecTestData.Entry();
        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) => OperationAcceptedSessionEntryCodecTestData.Equivalent((OperationAcceptedSessionEntry) expected, (OperationAcceptedSessionEntry) actual);
    }

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new OperationAcceptedSessionEntryCodec(null!, NullLogger<OperationAcceptedSessionEntryCodec>.Instance)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new OperationAcceptedSessionEntryCodec(TimeProvider.System, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, null!)).ParamName.ShouldBe("limits");
    }

    [Fact]
    public void EncodeDecode_WhenInputIsNull_ThrowsBeforeObservation()
    {
        var clock = new CountingClock();
        var logger = new CountingLogger();
        var codec = new OperationAcceptedSessionEntryCodec(clock, logger);
        using var parent = new Activity("accepted.boundary").SetIdFormat(ActivityIdFormat.W3C).Start();
        var started = false;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static s => s.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref o) => o.Name == AgentKitActivityNames.SessionEntryCodec && o.Parent == parent.Context ? ActivitySamplingResult.PropagationData : ActivitySamplingResult.None,
            ActivityStarted = a => started |= a.OperationName == AgentKitActivityNames.SessionEntryCodec && a.ParentSpanId == parent.SpanId && a.TraceId == parent.TraceId,
        };
        ActivitySource.AddActivityListener(listener);
        Should.Throw<ArgumentNullException>(() => codec.Encode(null!)).ParamName.ShouldBe("entry");
        Should.Throw<ArgumentNullException>(() => codec.Decode(null!)).ParamName.ShouldBe("wire");
        clock.Calls.ShouldBe(0);
        logger.Calls.ShouldBe(0);
        started.ShouldBeFalse();
    }

    [Fact]
    public void EncodeDecode_WhenPayloadLimitIsExactOrOneByteTighter_RespectsBound()
    {
        var baseline = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance);
        var entry = OperationAcceptedSessionEntryCodecTestData.Entry();
        var wire = baseline.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var exact = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length, 64, 65_536, 64));
        var tighter = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length - 1, 64, 65_536, 64));
        _ = exact.Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        _ = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        _ = tighter.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = tighter.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void EncodeDecode_WhenJsonDepthIsExactOrOneLevelTooShallow_RespectsBound()
    {
        var entry = OperationAcceptedSessionEntryCodecTestData.Entry();
        var exact = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(1_048_576, 64, 65_536, 7));
        var shallow = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(1_048_576, 64, 65_536, 6));
        var wire = exact.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        _ = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        _ = shallow.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = shallow.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenIdentityClaimUnknownEscapedNameIsAtRawByteAndCountBound_PreservesOriginalWire()
    {
        var baseline = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance);
        var encoded = baseline.Encode(OperationAcceptedSessionEntryCodecTestData.Entry()).ShouldBeOfType<SessionEntryEncoded>();
        const string known = "\"valueKind\":\"text\"";
        const string extension = "\"f\\u0075ture\":0,";
        var source = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan());
        var position = source.IndexOf(known, StringComparison.Ordinal);
        position.ShouldBeGreaterThanOrEqualTo(0);
        var payload = source.Insert(position, extension);
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(payload)]);
        var rawPropertyBytes = Encoding.UTF8.GetByteCount(extension[..^1]);
        var exact = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(1_048_576, 1, rawPropertyBytes, 24));
        var tighter = new OperationAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<OperationAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(1_048_576, 1, rawPropertyBytes - 1, 24));
        var decoded = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        decoded.Decoded.Wire.ShouldBeSameAs(wire);
        decoded.Decoded.Wire.Payload.AsSpan().SequenceEqual(wire.Payload.AsSpan()).ShouldBeTrue();
        _ = tighter.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    private sealed class CountingClock: TimeProvider
    {
        public int Calls { get; private set; }

        public override long GetTimestamp() => ++Calls;
    }

    private sealed class CountingLogger: ILogger<OperationAcceptedSessionEntryCodec>
    {
        public int Calls { get; private set; }

        public IDisposable? BeginScope<T>(T state)
            where T : notnull => null;
        public bool IsEnabled(LogLevel level) => ++Calls >= 0;
        public void Log<T>(LogLevel l, EventId e, T s, Exception? x, Func<T, Exception?, string> f) => Calls++;
    }
}
