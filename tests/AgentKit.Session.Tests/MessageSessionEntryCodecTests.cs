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
}
