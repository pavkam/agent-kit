// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;

using AgentKit.Conformance;

/// <summary>Verifies the durable input-admitted-entry codec preserves its schema and rejects corrupt payloads with typed results.</summary>
public sealed class InputAdmittedSessionEntryCodecTests: SessionEntryCodecConformanceTests<InputAdmittedSessionEntryCodecTests.Fixture>
{
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new InputAdmittedSessionEntryCodec();
        public SessionEntry CreateEntry() => AdmittedEntry();
        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual)
        {
            var left = (InputAdmittedSessionEntry) expected;
            var right = (InputAdmittedSessionEntry) actual;
            return left.Id == right.Id && left.Address == right.Address && left.Correlation == right.Correlation
                && left.BranchId == right.BranchId && left.Sequence == right.Sequence
                && left.CausalParentId == right.CausalParentId && left.RecordedAt == right.RecordedAt
                && left.SchemaVersion == right.SchemaVersion && Equivalent(left.Input, right.Input);
        }

        private static bool Equivalent(AdmittedInput left, AdmittedInput right) =>
            left.AdmissionId == right.AdmissionId && left.AgentId == right.AgentId && left.SessionId == right.SessionId
            && left.ExecutionLaneId == right.ExecutionLaneId && left.Identity == right.Identity
            && left.AdmittedSequence == right.AdmittedSequence && left.Preprocessing == right.Preprocessing
            && left.AdmittedAt == right.AdmittedAt && left.PromotedSequence == right.PromotedSequence
            && Equivalent(left.OriginalPayload, right.OriginalPayload)
            && Equivalent(left.EffectivePayload, right.EffectivePayload);

        private static bool Equivalent(AgentInput left, AgentInput right) =>
            left.Id == right.Id && left.Delivery == right.Delivery && left.Extensions == right.Extensions
            && left.Parts.SequenceEqual(right.Parts);
    }

    [Fact]
    public void Encode_WhenEntryIsNotAnInputAdmittedSessionEntry_RejectsBeforePersistence()
    {
        var result = new InputAdmittedSessionEntryCodec().Encode(PortableSessionEntryCodecTestEntries.Lane());

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The entry is not a input-admitted session entry.");
    }

    [Fact]
    public void Encode_WhenValueExceedsConfiguredJsonDepth_ReturnsRejected()
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < 100; i++)
        {
            _ = builder.Append("{\"a\":");
        }
        _ = builder.Append('1');
        for (var i = 0; i < 100; i++)
        {
            _ = builder.Append('}');
        }
        using var document = JsonDocument.Parse(builder.ToString(), new JsonDocumentOptions { MaxDepth = 200 });
        var entry = AdmittedEntry([new StructuredDataPart(document.RootElement.Clone(), null, ExtensionData.Empty)]);

        var result = new InputAdmittedSessionEntryCodec().Encode(entry);

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The input-admitted entry cannot be represented by the version-one schema.");
    }

    [Fact]
    public void Decode_WhenWireTypeIdDoesNotMatchCodec_ReturnsOpaque()
    {
        var codec = new InputAdmittedSessionEntryCodec();
        var wire = new SessionEntryWireEnvelope(new SessionEntryTypeId("other"), new SchemaVersion("1"), [1]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryOpaque>().Wire.ShouldBeSameAs(wire);
    }

    [Fact]
    public void Decode_WhenIdentityIsEmptyGuid_ReturnsRejected()
    {
        var codec = new InputAdmittedSessionEntryCodec();
        var encoded = codec.Encode(AdmittedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["Id"]!["Value"] = Guid.Empty;

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The input-admitted entry payload violates its invariants.");
    }

    [Fact]
    public void Decode_WhenValueObjectLacksValueProperty_ReturnsRejected()
    {
        var codec = new InputAdmittedSessionEntryCodec();
        var encoded = codec.Encode(AdmittedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["BranchId"] = new JsonObject();

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The input-admitted entry payload is malformed.");
    }

    [Fact]
    public void Decode_WhenRequiredFieldIsMissing_ReturnsRejected()
    {
        var codec = new InputAdmittedSessionEntryCodec();
        var encoded = codec.Encode(AdmittedEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        _ = payload.AsObject().Remove("Address");

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The input-admitted entry payload is malformed.");
    }

    [Fact]
    public void Decode_WhenPayloadIsNotAnObject_ReturnsRejected()
    {
        var codec = new InputAdmittedSessionEntryCodec();
        var encoded = codec.Encode(AdmittedEntry()).ShouldBeOfType<SessionEntryEncoded>();

        var result = codec.Decode(Rewire(encoded.Wire, JsonValue.Create(42)));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The input-admitted entry payload is malformed.");
    }

    [Fact]
    public void Encode_WhenStructuredValueIsUninitialized_ReturnsRejected()
    {
        var entry = AdmittedEntry([new StructuredDataPart(default, null, ExtensionData.Empty)]);

        var result = new InputAdmittedSessionEntryCodec().Encode(entry);

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The input-admitted entry carries a value that cannot be serialized.");
    }

    private static InputAdmittedSessionEntry AdmittedEntry(ImmutableArray<ContentPart>? parts = null)
    {
        var agentId = new AgentId(Id(2));
        var sessionId = new SessionId(Id(3));
        var address = new SessionAddress(agentId, sessionId);
        var sequence = new SessionSequence(1);
        var payload = new AgentInput(
            new InputId(Id(10)),
            InputDelivery.Steer,
            parts ?? [new TextPart("input", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var input = new AdmittedInput(
            new AdmissionId(Id(9)),
            agentId,
            sessionId,
            new ExecutionLaneId(Id(6)),
            TestFactory.Identity(),
            sequence,
            payload,
            payload,
            new InputPreprocessingManifest(new ConfigurationVersion(1), new InputFingerprint("original:1"), new InputFingerprint("effective:1")),
            DateTimeOffset.UnixEpoch);
        return new InputAdmittedSessionEntry(
            new SessionEntryId(Id(1)),
            address,
            new BeforeRunOperationCorrelation(new OperationId(Id(4)), null),
            new BranchId(Id(5)),
            sequence,
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            input);
    }

    private static SessionEntryWireEnvelope Rewire(SessionEntryWireEnvelope wire, JsonNode payload) => new(
        wire.TypeId,
        wire.SchemaVersion,
        [.. JsonSerializer.SerializeToUtf8Bytes(payload)]);

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
