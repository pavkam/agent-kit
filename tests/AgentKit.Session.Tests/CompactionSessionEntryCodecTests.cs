// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;

using AgentKit.Conformance;

/// <summary>Verifies the durable compaction-entry codec preserves its schema and rejects corrupt payloads with typed results.</summary>
public sealed class CompactionSessionEntryCodecTests: SessionEntryCodecConformanceTests<CompactionSessionEntryCodecTests.Fixture>
{
    public sealed class Fixture: ISessionEntryCodecConformanceFixture
    {
        public ISessionEntryCodec CreateCodec() => new CompactionSessionEntryCodec();
        public SessionEntry CreateEntry() => CompactionEntry();
        public bool SemanticallyEquivalent(SessionEntry expected, SessionEntry actual) => expected.Equals(actual);
    }

    [Fact]
    public void Encode_WhenEntryIsNotACompactionSessionEntry_RejectsBeforePersistence()
    {
        var result = new CompactionSessionEntryCodec().Encode(PortableSessionEntryCodecTestEntries.Lane());

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The entry is not a compaction session entry.");
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
        var entry = CompactionEntry([new StructuredDataPart(document.RootElement.Clone(), null, ExtensionData.Empty)]);

        var result = new CompactionSessionEntryCodec().Encode(entry);

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The compaction entry cannot be represented by the version-one schema.");
    }

    [Fact]
    public void Decode_WhenWireTypeIdDoesNotMatchCodec_ReturnsOpaque()
    {
        var codec = new CompactionSessionEntryCodec();
        var wire = new SessionEntryWireEnvelope(new SessionEntryTypeId("other"), new SchemaVersion("1"), [1]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryOpaque>().Wire.ShouldBeSameAs(wire);
    }

    [Fact]
    public void Decode_WhenIdentityIsEmptyGuid_ReturnsRejected()
    {
        var codec = new CompactionSessionEntryCodec();
        var encoded = codec.Encode(CompactionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["Id"]!["Value"] = Guid.Empty;

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The compaction entry payload violates its invariants.");
    }

    [Fact]
    public void Decode_WhenValueObjectLacksValueProperty_ReturnsRejected()
    {
        var codec = new CompactionSessionEntryCodec();
        var encoded = codec.Encode(CompactionEntry()).ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["Id"] = new JsonObject();

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The compaction entry payload is malformed.");
    }

    [Fact]
    public void Encode_WhenStructuredValueIsUninitialized_ReturnsRejected()
    {
        var entry = CompactionEntry([new StructuredDataPart(default, null, ExtensionData.Empty)]);

        var result = new CompactionSessionEntryCodec().Encode(entry);

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The compaction entry carries a value that cannot be serialized.");
    }

    private static CompactionSessionEntry CompactionEntry(ImmutableArray<ContentPart>? checkpointParts = null)
    {
        var agentId = new AgentId(Id(2));
        var sessionId = new SessionId(Id(3));
        var branchId = new BranchId(Id(5));
        var correlation = new InRunOperationCorrelation(new OperationId(Id(4)), new RunId(Id(7)), null);
        var context = TestSupport.TestSecurityEvidence.CompactionContext(
            new CompactionId(Id(8)), agentId, sessionId, correlation, TestFactory.Identity());
        var manifest = new CompactionManifest(
            new CompactionManifestId(Id(11)),
            context,
            branchId,
            new SessionVersion(1),
            new CompactionSourceRange(new SessionSequence(1), new SessionSequence(1)),
            new SessionSequence(2),
            new CompactionProducer(new CompactionStrategyKey("test"), true, ExtensionData.Empty),
            new ContextEpoch(0),
            new CompactionSizeEstimate(1, 1, 1),
            new CompactionSizeEstimate(1, 1, 1),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var checkpoint = new CompactionCheckpoint(
            checkpointParts ?? [new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var record = new CompactionRecord(
            context,
            new SessionVersion(1),
            new SessionVersion(2),
            CompactionRecordStatus.Active,
            manifest,
            checkpoint,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        return new CompactionSessionEntry(
            new SessionEntryId(Id(1)),
            new SessionAddress(agentId, sessionId),
            correlation,
            branchId,
            new SessionSequence(2),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            record);
    }

    private static SessionEntryWireEnvelope Rewire(SessionEntryWireEnvelope wire, JsonNode payload) => new(
        wire.TypeId,
        wire.SchemaVersion,
        [.. JsonSerializer.SerializeToUtf8Bytes(payload)]);

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
}
