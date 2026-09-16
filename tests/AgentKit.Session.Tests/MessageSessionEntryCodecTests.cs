// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Verifies the durable message-entry codec preserves its exact schema contract.</summary>
public sealed class MessageSessionEntryCodecTests
{
    [Fact]
    public void EncodeAndDecode_WhenEntryUsesSupportedSchema_PreservesEntryAndWire()
    {
        var descriptor = TestFactory.Descriptor();
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        var codec = new MessageSessionEntryCodec();

        var encoded = codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        var decoded = codec.Decode(encoded.Wire).ShouldBeOfType<SessionEntryDecoded>();

        decoded.Decoded.Wire.ShouldBeSameAs(encoded.Wire);
        decoded.Decoded.Entry.ShouldBe(entry);
        decoded.Decoded.Entry.SchemaVersion.ShouldBe(encoded.Wire.SchemaVersion);
    }

    [Fact]
    public void Encode_WhenEntryIsNotAMessageSessionEntry_RejectsBeforePersistence()
    {
        var result = new MessageSessionEntryCodec().Encode(PortableSessionEntryCodecTestEntries.Lane());

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The entry is not a message session entry.");
    }

    [Fact]
    public void Encode_WhenValueExceedsConfiguredJsonDepth_ReturnsRejected()
    {
        var descriptor = TestFactory.Descriptor();
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
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
        entry = entry with
        {
            Message = entry.Message with
            {
                Parts = [new StructuredDataPart(document.RootElement.Clone(), null, ExtensionData.Empty)],
            },
        };

        var result = new MessageSessionEntryCodec().Encode(entry);

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The message entry cannot be represented by the version-one schema.");
    }

    [Fact]
    public void Decode_WhenWireTypeIdDoesNotMatchCodec_ReturnsOpaque()
    {
        var codec = new MessageSessionEntryCodec();
        var wire = new SessionEntryWireEnvelope(new SessionEntryTypeId("other"), new SchemaVersion("1"), [1]);

        var result = codec.Decode(wire);

        result.ShouldBeOfType<SessionEntryOpaque>().Wire.ShouldBeSameAs(wire);
    }

    [Fact]
    public void Encode_WhenEntrySchemaDiffersFromCodecSchema_RejectsBeforePersistence()
    {
        var descriptor = TestFactory.Descriptor();
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1) with
        {
            SchemaVersion = new SchemaVersion("1.0"),
        };

        var result = new MessageSessionEntryCodec().Encode(entry);

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The message entry schema is not supported by this codec.");
    }

    [Fact]
    public void Decode_WhenPayloadEntrySchemaDiffersFromWireSchema_Rejects()
    {
        var descriptor = TestFactory.Descriptor();
        var codec = new MessageSessionEntryCodec();
        var encoded = codec.Encode(TestFactory.MessageEntry(
            descriptor.Address,
            descriptor.ActiveBranchId,
            1)).ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["SchemaVersion"]!["Value"] = "1.0";
        var mismatchedWire = new SessionEntryWireEnvelope(
            encoded.Wire.TypeId,
            encoded.Wire.SchemaVersion,
            [.. JsonSerializer.SerializeToUtf8Bytes(payload)]);

        var result = codec.Decode(mismatchedWire);

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The message entry schema does not match its wire envelope.");
    }

    [Fact]
    public void Decode_WhenIdentityIsEmptyGuid_ReturnsRejected()
    {
        var descriptor = TestFactory.Descriptor();
        var codec = new MessageSessionEntryCodec();
        var encoded = codec.Encode(TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1))
            .ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["Id"]!["Value"] = Guid.Empty;

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The message entry payload violates its invariants.");
    }

    [Fact]
    public void Decode_WhenValueObjectLacksValueProperty_ReturnsRejected()
    {
        var descriptor = TestFactory.Descriptor();
        var codec = new MessageSessionEntryCodec();
        var encoded = codec.Encode(TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1))
            .ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["Id"] = new JsonObject();

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The message entry payload is malformed.");
    }

    [Fact]
    public void Decode_WhenToolIdIsBlank_ReturnsRejected()
    {
        var descriptor = TestFactory.Descriptor();
        var codec = new MessageSessionEntryCodec();
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        var toolCall = new ToolCallPart(
            new ToolCallId(Guid.NewGuid()),
            new ToolReference(new ToolAlias("search"), new ToolId("search"), new ToolVersion("1")),
            JsonDocument.Parse("{}").RootElement.Clone(),
            null,
            ExtensionData.Empty);
        entry = entry with { Message = entry.Message with { Parts = [toolCall] } };
        var encoded = codec.Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        var payload = JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
        payload["Message"]!["Parts"]![0]!["Tool"]!["Id"]!["Value"] = " ";

        var result = codec.Decode(Rewire(encoded.Wire, payload));

        result.ShouldBeOfType<SessionEntryDecodeRejected>().Reason.ShouldBe(
            "The message entry payload violates its invariants.");
    }

    [Fact]
    public void Encode_WhenStructuredValueIsUninitialized_ReturnsRejected()
    {
        var descriptor = TestFactory.Descriptor();
        var entry = TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1);
        entry = entry with
        {
            Message = entry.Message with { Parts = [new StructuredDataPart(default, null, ExtensionData.Empty)] },
        };

        var result = new MessageSessionEntryCodec().Encode(entry);

        result.ShouldBeOfType<SessionEntryEncodeRejected>().Reason.ShouldBe(
            "The message entry carries a value that cannot be serialized.");
    }

    private static SessionEntryWireEnvelope Rewire(SessionEntryWireEnvelope wire, JsonNode payload) => new(
        wire.TypeId,
        wire.SchemaVersion,
        [.. JsonSerializer.SerializeToUtf8Bytes(payload)]);
}
