// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using System.Text.Json;

using AgentKit.Session;

/// <summary>Verifies the durable wire-envelope converter delegates to the injected codec catalog for every outcome.</summary>
/// <remarks>
/// The converter owns no entry format of its own, so these cases drive every typed catalog outcome directly rather than
/// only the happy path. Round trips for the other first-party entry kinds are exercised through real store durability
/// scenarios in <see cref="JsonSessionStoreTests"/>, since those entries require a fully populated session context.
/// </remarks>
public sealed class JsonSessionEntryConverterTests
{
    /// <summary>Verifies a null codec catalog is rejected.</summary>
    [Fact]
    public void Constructor_WhenCodecsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new JsonSessionEntryConverter(null!)).ParamName.ShouldBe("codecs");

    /// <summary>Verifies a message entry survives being written by one catalog and read by a freshly recreated one.</summary>
    [Fact]
    public void ReadAndWrite_WhenCatalogIsRecreated_PreservesMessageEntry()
    {
        var entry = MessageEntry();
        var options = Options(Catalog());

        var persisted = JsonSerializer.Serialize<SessionEntry>(entry, options);
        var reopened = JsonSerializer.Deserialize<SessionEntry>(persisted, Options(Catalog()));

        reopened.ShouldBe(entry);
        reopened.ShouldBeOfType<MessageSessionEntry>().SchemaVersion.ShouldBe(new SchemaVersion("1"));
    }

    /// <summary>Verifies the emitted envelope carries exactly the portable typeId/schemaVersion/payload shape.</summary>
    [Fact]
    public void Write_WhenCalled_EmitsThePortableEnvelopeShape()
    {
        var persisted = JsonSerializer.Serialize<SessionEntry>(MessageEntry(), Options(Catalog()));

        using var document = JsonDocument.Parse(persisted);
        var propertyNames = document.RootElement.EnumerateObject().Select(static property => property.Name).ToArray();

        propertyNames.ShouldBe(["typeId", "schemaVersion", "payload"]);
    }

    /// <summary>Verifies a root that is not a JSON object is rejected as an incomplete envelope.</summary>
    [Fact]
    public void Read_WhenRootIsNotAnObject_ThrowsJsonException()
    {
        var options = Options(Catalog());

        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SessionEntry>("[]", options));

        exception.Message.ShouldBe("A persisted session entry envelope is incomplete.");
    }

    /// <summary>Verifies a missing required envelope property is rejected as incomplete.</summary>
    [Theory]
    [InlineData(/*lang=json,strict*/ """{"schemaVersion":"1","payload":"AA=="}""")]
    [InlineData(/*lang=json,strict*/ """{"typeId":"agentkit.message","payload":"AA=="}""")]
    [InlineData(/*lang=json,strict*/ """{"typeId":"agentkit.message","schemaVersion":"1"}""")]
    public void Read_WhenARequiredPropertyIsMissing_ThrowsJsonException(string malformed)
    {
        var options = Options(Catalog());

        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SessionEntry>(malformed, options));

        exception.Message.ShouldBe("A persisted session entry envelope is incomplete.");
    }

    /// <summary>Verifies a payload that is not valid base-64 is rejected with the original format failure retained.</summary>
    [Fact]
    public void Read_WhenPayloadIsNotValidBase64_ThrowsJsonExceptionWithInnerFormatException()
    {
        var malformed = """{"typeId":"agentkit.message","schemaVersion":"1","payload":"not-base64!!"}""";
        var options = Options(Catalog());

        var exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<SessionEntry>(malformed, options));

        exception.Message.ShouldBe("A persisted session entry payload is not valid base-64.");
        _ = exception.InnerException.ShouldBeOfType<FormatException>();
    }

    /// <summary>Verifies a codec's typed decode rejection surfaces its exact reason.</summary>
    [Fact]
    public void Read_WhenCodecReportsDecodeRejected_ThrowsJsonExceptionWithReason()
    {
        var codecs = new FixedResultCodecCatalog(decodeResult: new SessionEntryDecodeRejected("malformed payload"));
        var persisted = JsonSerializer.Serialize<SessionEntry>(MessageEntry(), Options(Catalog()));

        var exception = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<SessionEntry>(persisted, Options(codecs)));

        exception.Message.ShouldBe("malformed payload");
    }

    /// <summary>Verifies an opaque decode (unknown type or schema) is rejected rather than silently skipped.</summary>
    [Fact]
    public void Read_WhenCodecReportsOpaque_ThrowsJsonException()
    {
        var persisted = JsonSerializer.Serialize<SessionEntry>(MessageEntry(), Options(Catalog()));
        var codecs = new FixedResultCodecCatalog(
            decodeResult: new SessionEntryOpaque(
                new SessionEntryWireEnvelope(new SessionEntryTypeId("unknown"), new SchemaVersion("1"), [0])));

        var exception = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<SessionEntry>(persisted, Options(codecs)));

        exception.Message.ShouldBe("The persisted session entry codec is unavailable.");
    }

    /// <summary>Verifies a decode outcome outside the converter's known closed set is rejected rather than assumed valid.</summary>
    [Fact]
    public void Read_WhenCodecReportsAnUnrecognizedDecodeResult_ThrowsJsonException()
    {
        var persisted = JsonSerializer.Serialize<SessionEntry>(MessageEntry(), Options(Catalog()));
        var codecs = new FixedResultCodecCatalog(decodeResult: new UnrecognizedDecodeResult());

        var exception = Should.Throw<JsonException>(
            () => JsonSerializer.Deserialize<SessionEntry>(persisted, Options(codecs)));

        exception.Message.ShouldBe("The persisted session entry decode outcome is unsupported.");
    }

    /// <summary>Verifies a codec's typed encode rejection is rejected rather than writing an unusable envelope.</summary>
    [Fact]
    public void Write_WhenCodecRejectsEncode_ThrowsJsonException()
    {
        var codecs = new FixedResultCodecCatalog(encodeResult: new SessionEntryEncodeRejected("no codec"));

        var exception = Should.Throw<JsonException>(
            () => JsonSerializer.Serialize<SessionEntry>(MessageEntry(), Options(codecs)));

        exception.Message.ShouldBe("The session entry has no durable codec.");
    }

    private static SessionEntryCodecCatalog Catalog() => new([new MessageSessionEntryCodec()], TimeProvider.System);

    private static JsonSerializerOptions Options(ISessionEntryCodecCatalog catalog)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonSessionEntryConverter(catalog));
        return options;
    }

    /// <summary>A codec catalog that returns a fixed configured result, to drive the converter's typed failure paths.</summary>
    private sealed class FixedResultCodecCatalog(
        SessionEntryEncodeResult? encodeResult = null, SessionEntryDecodeResult? decodeResult = null)
        : ISessionEntryCodecCatalog
    {
        public SessionEntryEncodeResult Encode(SessionEntry entry) =>
            encodeResult ?? throw new InvalidOperationException("This test catalog does not encode.");

        public SessionEntryDecodeResult Decode(SessionEntryWireEnvelope wire) =>
            decodeResult ?? throw new InvalidOperationException("This test catalog does not decode.");
    }

    /// <summary>A decode result outside the converter's known closed set, to drive its exhaustive-switch default arm.</summary>
    private sealed record UnrecognizedDecodeResult: SessionEntryDecodeResult;

    private static MessageSessionEntry MessageEntry()
    {
        var agentId = JsonSessionStoreTests.Identifier<AgentId>(400);
        var sessionId = JsonSessionStoreTests.Identifier<SessionId>(401);
        var branchId = new BranchId(new Guid(402, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var address = new SessionAddress(agentId, sessionId);
        return new MessageSessionEntry(
            JsonSessionStoreTests.Identifier<SessionEntryId>(403),
            address,
            new InRunOperationCorrelation(
                JsonSessionStoreTests.Identifier<OperationId>(404), JsonSessionStoreTests.Identifier<RunId>(405), null),
            branchId,
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            new UserMessage(
                JsonSessionStoreTests.Identifier<MessageId>(406),
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
