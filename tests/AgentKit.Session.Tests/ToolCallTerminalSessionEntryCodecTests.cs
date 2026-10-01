// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Text;
using System.Text.Json.Nodes;

using AgentKit.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies ToolCallTerminalSessionEntryCodec behavior and contracts.</summary>
public sealed class ToolCallTerminalSessionEntryCodecTests: SessionEntryCodecConformanceTests<ToolCallTerminalSessionEntryCodecTests.Fixture>
{
    [Fact]
    public void EncodeDecode_WhenEntryIsComplete_PreservesEveryFieldAndTheOriginalWire()
    {
        var codec = Codec();
        var expected = ToolCallSessionEntryCodecTestData.TerminalEntry();
        var wire = codec.Encode(expected).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var decoded = codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;
        var actual = decoded.Entry.ShouldBeOfType<ToolCallTerminalSessionEntry>();
        ToolCallSessionEntryCodecTestData.Equivalent(expected, actual).ShouldBeTrue();
        actual.RecordedAt.Offset.ShouldBe(TimeSpan.FromMinutes(90));
        decoded.Wire.ShouldBeSameAs(wire);
        wire.TypeId.ShouldBe(new SessionEntryTypeId("agentkit.session/tool-call-terminal"));
        wire.SchemaVersion.ShouldBe(new SchemaVersion("1"));
    }

    [Fact]
    public void Decode_WhenUnknownNestedEvidenceIsWithinBounds_PreservesExactWire()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.TerminalEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var json = Encoding.UTF8.GetString(encoded.Wire.Payload.AsSpan()).Replace("\"providerAlias\":", "\"future\":{\"x\":1},\"providerAlias\":", StringComparison.Ordinal);
        var wire = new SessionEntryWireEnvelope(encoded.Wire.TypeId, encoded.Wire.SchemaVersion, [.. Encoding.UTF8.GetBytes(json)]);

        codec.Decode(wire).ShouldBeOfType<SessionEntryDecoded>().Decoded.Wire.ShouldBeSameAs(wire);
    }

    [Fact]
    public void Decode_WhenCallCorrelationDiffersFromTheAuthorizationScope_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.TerminalEntry()).ShouldBeOfType<SessionEntryEncoded>();
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
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.TerminalEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var root = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        root[field] = value;

        _ = codec.Decode(Wire(encoded.Wire, root)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenRequiredFieldIsMissing_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.TerminalEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var root = JsonNode.Parse(encoded.Wire.Payload.AsSpan())!.AsObject();
        _ = root["state"]!["call"]!.AsObject().Remove("admission");

        _ = codec.Decode(Wire(encoded.Wire, root)).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Decode_WhenWireIdentityIsForeign_ReturnsTypedRejection()
    {
        var codec = Codec();
        var encoded = codec.Encode(ToolCallSessionEntryCodecTestData.TerminalEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var foreign = new SessionEntryWireEnvelope(new SessionEntryTypeId("other/type"), encoded.Wire.SchemaVersion, encoded.Wire.Payload);

        _ = codec.Decode(foreign).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Encode_WhenEntryIsOfAnotherType_ReturnsTypedRejection() =>
        _ = Codec().Encode(PortableSessionEntryCodecTestEntries.Lane()).ShouldBeOfType<SessionEntryEncodeRejected>();

    [Fact]
    public void Encode_WhenIdentityIsDefault_ReturnsTypedRejection()
    {
        var malformed = ToolCallSessionEntryCodecTestData.TerminalEntry() with { Id = default };

        _ = Codec().Encode(malformed).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Encode_WhenRetainedTextIsMalformed_RejectsWithoutReplacement()
    {
        var malformed = ToolCallSessionEntryCodecTestData.TerminalEntry(ToolCallSessionEntryCodecTestData.Terminal(error: new ToolError(ToolErrorKind.Tool, "bad" + '\uD800', null, null, ExtensionData.Empty)));

        _ = Codec().Encode(malformed).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    [Fact]
    public void Operations_WhenPayloadLimitIsExactOrOneByteTighter_RespectsBound()
    {
        var entry = ToolCallSessionEntryCodecTestData.TerminalEntry();
        var wire = Codec().Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire;
        var exact = new ToolCallTerminalSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallTerminalSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length, 64, 65_536, 64));
        var tighter = new ToolCallTerminalSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallTerminalSessionEntryCodec>.Instance, new SessionEntryCodecLimits(wire.Payload.Length - 1, 64, 65_536, 64));

        _ = exact.Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        _ = exact.Decode(wire).ShouldBeOfType<SessionEntryDecoded>();
        _ = tighter.Encode(entry).ShouldBeOfType<SessionEntryEncodeRejected>();
        _ = tighter.Decode(wire).ShouldBeOfType<SessionEntryDecodeRejected>();
    }

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new ToolCallTerminalSessionEntryCodec(null!, NullLogger<ToolCallTerminalSessionEntryCodec>.Instance)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ToolCallTerminalSessionEntryCodec(TimeProvider.System, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentNullException>(() => new ToolCallTerminalSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallTerminalSessionEntryCodec>.Instance, null!)).ParamName.ShouldBe("limits");
    }

    [Fact]
    public void EncodeDecode_WhenInputIsNull_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => Codec().Encode(null!)).ParamName.ShouldBe("entry");
        Should.Throw<ArgumentNullException>(() => Codec().Decode(null!)).ParamName.ShouldBe("wire");
    }

    [Fact]
    public void EncodeDecode_WhenCallWasRejectedBeforeAcceptance_RoundTripsAbsentEvidence()
    {
        var codec = Codec();
        var entry = ToolCallSessionEntryCodecTestData.TerminalEntry(ToolCallSessionEntryCodecTestData.Terminal(accepted: false, status: ToolTerminalStatus.UnknownTool));
        var decoded = codec.Decode(codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;

        var result = decoded.Entry.ShouldBeOfType<ToolCallTerminalSessionEntry>().Result;
        result.Acceptance.ShouldBeNull();
        result.ToolId.ShouldBeNull();
        result.Effects.ShouldBeNull();
        result.Normalization.ExecutionPolicy.ShouldBeNull();
    }

    [Fact]
    public void EncodeDecode_WhenStatusIsAFutureNumericValue_RetainsTheExactValue()
    {
        var codec = Codec();
        var entry = ToolCallSessionEntryCodecTestData.TerminalEntry(ToolCallSessionEntryCodecTestData.Terminal(status: (ToolTerminalStatus) 4242));
        var decoded = codec.Decode(codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;

        ((int) decoded.Entry.ShouldBeOfType<ToolCallTerminalSessionEntry>().Result.Status).ShouldBe(4242);
    }

    [Fact]
    public void EncodeDecode_WhenSafeErrorCarriesRetryAfter_PreservesIt()
    {
        var codec = Codec();
        var entry = ToolCallSessionEntryCodecTestData.TerminalEntry();
        var decoded = codec.Decode(codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>().Wire).ShouldBeOfType<SessionEntryDecoded>().Decoded;

        var error = decoded.Entry.ShouldBeOfType<ToolCallTerminalSessionEntry>().Result.Error.ShouldNotBeNull();
        error.RetryAfter.ShouldBe(TimeSpan.FromSeconds(3));
        error.ExternalCode.ShouldBe("E_FAIL");
    }

    [Fact]
    public void Encode_WhenResultCarriesUsage_ReturnsTypedRejection()
    {
        var usage = new ToolUsage(
            [new ToolUsageMeasurement(new BudgetDimension("tokens"), new BudgetUnit("count"), BudgetQuantity.FromDecimal(1), ToolUsageMeasurementQuality.Measured)],
            ExtensionData.Empty);

        _ = Codec().Encode(ToolCallSessionEntryCodecTestData.TerminalEntry(ToolCallSessionEntryCodecTestData.Terminal(usage: usage))).ShouldBeOfType<SessionEntryEncodeRejected>();
    }

    private static ToolCallTerminalSessionEntryCodec Codec() => new(TimeProvider.System, NullLogger<ToolCallTerminalSessionEntryCodec>.Instance);

    private static SessionEntryWireEnvelope Wire(SessionEntryWireEnvelope template, JsonObject root) =>
        new(template.TypeId, template.SchemaVersion, [.. Encoding.UTF8.GetBytes(root.ToJsonString())]);

    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new ToolCallTerminalSessionEntryCodec(TimeProvider.System, NullLogger<ToolCallTerminalSessionEntryCodec>.Instance);

        public SessionEntry CreateEntry() => ToolCallSessionEntryCodecTestData.TerminalEntry();

        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) =>
            ToolCallSessionEntryCodecTestData.Equivalent((ToolCallTerminalSessionEntry) expected, (ToolCallTerminalSessionEntry) actual);
    }
}
