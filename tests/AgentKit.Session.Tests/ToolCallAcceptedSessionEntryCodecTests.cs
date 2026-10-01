// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Text;
using System.Text.Json.Nodes;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies ToolCallAcceptedSessionEntryCodec behavior and contracts.</summary>
public sealed class ToolCallAcceptedSessionEntryCodecTests: SessionEntryCodecConformanceTests<ToolCallAcceptedSessionEntryCodecTests.Fixture>
{
    [Fact]
    public void EncodeDecode_WhenEntryIsComplete_PreservesEveryFieldAndTheOriginalWire()
    {
        var codec = Codec();
        var expected = ToolCallSessionEntryCodecTestData.AcceptedEntry();
        var wire = codec.Encode(expected).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var decoded = codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;
        var actual = decoded.Entry.ShouldBeOfType<ToolCallAcceptedSessionEntry>();
        ToolCallSessionEntryCodecTestData.Equivalent(expected, actual).ShouldBeTrue();
        actual.RecordedAt.Offset.ShouldBe(TimeSpan.FromMinutes(90));
        decoded.Wire.ShouldBeSameAs(wire);
        wire.TypeId.ShouldBe(new SessionEntryTypeId("agentkit.session/tool-call-accepted"));
        wire.SchemaVersion.ShouldBe(new SchemaVersion("1"));
    }

    [Fact]
    public void Decode_WhenUnknownNestedEvidenceIsWithinBounds_PreservesExactWire()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.AcceptedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).Replace("\"providerAlias\":", "\"future\":{\"x\":1},\"providerAlias\":", StringComparison.Ordinal);
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(json)]);

        codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>().Decoded.Wire.ShouldBeSameAs(wire);
    }

    [Fact]
    public void Decode_WhenCallCorrelationDiffersFromTheAuthorizationScope_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.AcceptedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var root = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        root["state"]!["correlation"]!["turnId"] = "00000000-0000-0000-0000-0000000000ff";

        _ = codec.Decode(Wire(encoded.Wire, root)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Theory]
    [InlineData("sequence", 0)]
    [InlineData("sequence", -1)]
    public void Decode_WhenEnvelopeNumberIsOutOfRange_ReturnsTypedRejection(string field, long value)
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.AcceptedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var root = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        root[field] = value;

        _ = codec.Decode(Wire(encoded.Wire, root)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenRequiredFieldIsMissing_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.AcceptedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var root = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        _ = root["state"]!["call"]!.AsObject().Remove("admission");

        _ = codec.Decode(Wire(encoded.Wire, root)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenWireIdentityIsForeign_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.AcceptedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var foreign = new SessionEntryWireEnvelope(new SessionEntryTypeId("other/type"), encoded.Wire.SchemaVersion, encoded.Wire.Payload);

        _ = codec.Decode(foreign).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Encode_WhenEntryIsOfAnotherType_ReturnsTypedRejection() =>
        _ = Codec().Encode(PortableSessionEntryCodecTestEntries.Lane()).ShouldBeOfType<SessionEntryEncodeRejected>();

    [Fact]
    public void Encode_WhenIdentityIsDefault_ReturnsTypedRejection()
    {
        var malformed = ToolCallSessionEntryCodecTestData.AcceptedEntry() with { Id = default };

        _ = Codec().Encode(malformed).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Encode_WhenRetainedTextIsMalformed_RejectsWithoutReplacement()
    {
        var malformed = ToolCallSessionEntryCodecTestData.AcceptedEntry(ToolCallSessionEntryCodecTestData.Accepted(alias: "alias" + '\uD800'));

        _ = Codec().Encode(malformed).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Operations_WhenPayloadLimitIsExactOrOneByteTighter_RespectsBound()
    {
        var entry = ToolCallSessionEntryCodecTestData.AcceptedEntry();
        var wire = Codec().Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var exact = new ToolCallAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length, 64, 65_536, 64));
        var tighter = new ToolCallAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallAcceptedSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length - 1, 64, 65_536, 64));

        _ = exact.Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        _ = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        _ = tighter.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = tighter.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new ToolCallAcceptedSessionEntryCodec(null!, NullLogger<ToolCallAcceptedSessionEntryCodec>.Instance)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolCallAcceptedSessionEntryCodec(TimeProvider.System, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new ToolCallAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallAcceptedSessionEntryCodec>.Instance, null!)).ParamName.ShouldBe("limits");
    }

    [Fact]
    public void EncodeDecode_WhenInputIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => Codec().Encode(null!)).ParamName.ShouldBe("entry");
        Should.Throw<ArgumentNullException>(() => Codec().Decode(null!)).ParamName.ShouldBe("wire");
    }

    [Fact]
    public void Encode_WhenNormalizationCarriesExtensionData_ReturnsTypedRejection()
    {
        var baseline = ToolCallSessionEntryCodecTestData.Accepted();
        var extension = new ExtensionData(ImmutableDictionary<string, ExtensionValue>.Empty.Add(
            "x", new ExtensionValue([(byte) '1'])));
        var withExtensions = new AcceptedToolCall(
            baseline.AgentId, baseline.SessionId, baseline.RunId, baseline.TurnId, baseline.OperationId, baseline.CallId, baseline.Authorization,
            baseline.Acceptance, baseline.ProviderAlias, baseline.ToolId, baseline.ToolVersion, baseline.Effects, baseline.ExternalIdempotencyKey,
            baseline.Admission,
            new ToolResultNormalizationSnapshot(
                baseline.Normalization.RejectionPolicy, baseline.Normalization.ProjectionPolicy, baseline.Normalization.ExecutionPolicy,
                baseline.Normalization.AlgorithmVersion, baseline.Normalization.Bounds, baseline.Normalization.AllowedTransformations, extension),
            baseline.ProjectionPolicy, baseline.RequestedAt);

        _ = Codec().Encode(ToolCallSessionEntryCodecTestData.AcceptedEntry(withExtensions)).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void EncodeDecode_WhenEffectsAreReadOnlyWithoutIdempotency_RoundTripsAbsentClassification()
    {
        var codec = Codec();
        var entry = ToolCallSessionEntryCodecTestData.AcceptedEntry(ToolCallSessionEntryCodecTestData.Accepted(ToolEffect.ReadOnly, idempotency: null));
        var decoded = codec.Decode(codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;

        var call = decoded.Entry.ShouldBeOfType<ToolCallAcceptedSessionEntry>().Call;
        call.Effects.Idempotency.ShouldBeNull();
        call.ExternalIdempotencyKey.ShouldBeNull();
    }

    private static ToolCallAcceptedSessionEntryCodec Codec() => new(TimeProvider.System, NullLogger<ToolCallAcceptedSessionEntryCodec>.Instance);

    private static SessionEntryWireEnvelope Wire(SessionEntryWireEnvelope template, JsonObject root) =>
        new(template.TypeId, template.SchemaVersion, [.. Encoding.UTF8.GetBytes(root.ToJsonString())]);

    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new ToolCallAcceptedSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallAcceptedSessionEntryCodec>.Instance);

        public SessionEntry CreateEntry() => ToolCallSessionEntryCodecTestData.AcceptedEntry();

        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) =>
            ToolCallSessionEntryCodecTestData.Equivalent((ToolCallAcceptedSessionEntry) expected, (ToolCallAcceptedSessionEntry) actual);
    }
}
