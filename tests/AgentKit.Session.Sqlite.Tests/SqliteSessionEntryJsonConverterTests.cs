// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using System.Text.Json;

using AgentKit.Session;
using AgentKit.Session.Sqlite;

/// <summary>Verifies durable entry envelopes can be decoded by a freshly composed codec catalog.</summary>
public sealed class SqliteSessionEntryJsonConverterTests
{
    [Fact]
    public void ReadAndWrite_WhenCatalogIsRecreated_PreservesMessageEntry()
    {
        var entry = MessageEntry();
        var firstOptions = Options(Catalog());
        var persisted = JsonSerializer.Serialize<SessionEntry>(entry, firstOptions);
        var reopenedOptions = Options(Catalog());

        var reopened = JsonSerializer.Deserialize<SessionEntry>(persisted, reopenedOptions);

        reopened.ShouldBe(entry);
        reopened.ShouldBeOfType<MessageSessionEntry>().SchemaVersion.ShouldBe(new SchemaVersion("1"));
    }

    [Fact]
    public void Read_WhenCodecReportsDecodeRejected_ThrowsJsonExceptionWithReason()
    {
        var codecs = new FixedResultCodecCatalog(decodeResult: new SessionEntryDecodeRejected("malformed payload"));
        var options = Options(codecs);
        var persisted = JsonSerializer.Serialize<SessionEntry>(MessageEntry(), Options(Catalog()));

        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SessionEntry>(persisted, options));

        exception.Message.ShouldBe("malformed payload");
    }

    [Fact]
    public void Read_WhenCodecReportsOpaque_ThrowsJsonException()
    {
        var persisted = JsonSerializer.Serialize<SessionEntry>(MessageEntry(), Options(Catalog()));
        var codecs = new FixedResultCodecCatalog(
            decodeResult: new SessionEntryOpaque(
                new SessionEntryWireEnvelope(new SessionEntryTypeId("unknown"), new SchemaVersion("1"), [0])));
        var options = Options(codecs);

        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SessionEntry>(persisted, options));

        exception.Message.ShouldBe("The persisted session entry codec is unavailable.");
    }

    [Fact]
    public void Write_WhenCodecRejectsEncode_ThrowsJsonException()
    {
        var codecs = new FixedResultCodecCatalog(encodeResult: new SessionEntryEncodeRejected("no codec"));
        var options = Options(codecs);

        var exception = Should.Throw<JsonException>(() => JsonSerializer.Serialize<SessionEntry>(MessageEntry(), options));

        exception.Message.ShouldBe("The session entry has no durable codec.");
    }

    private static SessionEntryCodecCatalog Catalog() => new(
        [new MessageSessionEntryCodec()],
        TimeProvider.System);

    private static JsonSerializerOptions Options(ISessionEntryCodecCatalog catalog)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SqliteSessionEntryJsonConverterFactory(catalog));
        return options;
    }

    /// <summary>A codec catalog that returns a fixed configured result, to drive the converter's typed failure paths.</summary>
    private sealed class FixedResultCodecCatalog(
        SessionEntryEncodeResult? encodeResult = null, SessionEntryDecodeResult? decodeResult = null): ISessionEntryCodecCatalog
    {
        public SessionEntryEncodeResult Encode(SessionEntry entry) =>
            encodeResult ?? throw new InvalidOperationException("This test catalog does not encode.");

        public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire) =>
            decodeResult ?? throw new InvalidOperationException("This test catalog does not decode.");
    }

    private static MessageSessionEntry MessageEntry()
    {
        var agentId = new AgentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var sessionId = new SessionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var branchId = new BranchId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var address = new SessionAddress(agentId, sessionId);
        return new MessageSessionEntry(
            new SessionEntryId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            address,
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
                new RunId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
                null),
            branchId,
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new UserMessage(
                new MessageId(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                agentId,
                sessionId,
                null,
                branchId,
                null,
                null,
                DateTimeOffset.UnixEpoch,
                MessageState.Complete,
                [new TextPart("durable", TextSemantics.Plain, ExtensionData.Empty)],
                ExtensionData.Empty));
    }
}
