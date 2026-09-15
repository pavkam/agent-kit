// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using AgentKit.Session;

/// <summary>Verifies the SQLite state resolver shares the portable session discriminator map with the entry codecs.</summary>
public sealed class SqliteSessionJsonTypeResolverTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SessionId _sessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly BranchId _branchId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    public static TheoryData<ContentPart> Parts() =>
    [
        new TextPart("text", TextSemantics.Plain, ExtensionData.Empty),
        new StructuredDataPart(JsonDocument.Parse("""{"a":1}""").RootElement.Clone(), null, ExtensionData.Empty),
        new ToolCallPart(new ToolCallId(Guid.NewGuid()), Tool(), JsonDocument.Parse("{}").RootElement.Clone(), null, ExtensionData.Empty),
        new ToolResultPart(new ToolCallId(Guid.NewGuid()), Tool(), Outcome(), [], ExtensionData.Empty),
        new ReasoningPart(new ReasoningContent("why", ReasoningVisibility.Visible, null, ExtensionData.Empty), ExtensionData.Empty),
        new MediaReferencePart(
            new MediaReference(new MediaId(Guid.NewGuid()), MediaSourceKind.InlineBytes, "image/png", null, [1, 2], 2, null, ExtensionData.Empty),
            MediaSemantics.Input,
            ExtensionData.Empty),
        new UnknownContentPart("vendor.thing", JsonDocument.Parse("{}").RootElement.Clone(), ExtensionData.Empty),
    ];

    public static TheoryData<AgentMessage> Messages() =>
    [
        new SystemMessage(new MessageId(Guid.NewGuid()), _agentId, _sessionId, null, _branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [], ExtensionData.Empty),
        new DeveloperMessage(new MessageId(Guid.NewGuid()), _agentId, _sessionId, null, _branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [], ExtensionData.Empty),
        new UserMessage(new MessageId(Guid.NewGuid()), _agentId, _sessionId, null, _branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [], ExtensionData.Empty),
        new AssistantMessage(new MessageId(Guid.NewGuid()), _agentId, _sessionId, null, _branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [], Response(), ExtensionData.Empty),
        new ToolMessage(new MessageId(Guid.NewGuid()), _agentId, _sessionId, null, _branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [], ExtensionData.Empty),
        new RuntimeMessage(new MessageId(Guid.NewGuid()), _agentId, _sessionId, null, _branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [], ExtensionData.Empty),
    ];

    [Theory]
    [MemberData(nameof(Parts))]
    public void Create_WhenSerializingContentPart_UsesSameDiscriminatorAsMessageCodec(ContentPart part)
    {
        var codecKind = Discriminator(CodecPayload(part)["Message"]!["Parts"]![0]!);
        var sqliteKind = Discriminator(JsonNode.Parse(JsonSerializer.Serialize(part, SqliteOptions()))!);

        sqliteKind.ShouldBe(codecKind);
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Create_WhenSerializingAgentMessage_UsesSameDiscriminatorAsMessageCodec(AgentMessage message)
    {
        var codecKind = Discriminator(CodecPayload(message)["Message"]!);
        var sqliteKind = Discriminator(JsonNode.Parse(JsonSerializer.Serialize(message, SqliteOptions()))!);

        sqliteKind.ShouldBe(codecKind);
    }

    [Fact]
    public void Create_WhenDiscriminatorIsUnknown_FailsDeserialization()
    {
        var options = SqliteOptions();

        _ = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ContentPart>("""{"$kind":"json","Value":{}}""", options));
    }

    [Fact]
    public void Create_WhenComparedToPortablePolymorphism_DeclaresIdenticalDerivedTypes()
    {
        var resolver = SqliteSessionJsonTypeResolver.Create();
        var options = new JsonSerializerOptions();

        foreach (var (type, expected) in new[]
        {
            (typeof(ContentPart), PortableSessionJsonPolymorphism.Parts()),
            (typeof(AgentMessage), PortableSessionJsonPolymorphism.Messages()),
            (typeof(OperationCorrelation), PortableSessionJsonPolymorphism.Correlations()),
        })
        {
            var actual = resolver.GetTypeInfo(type, options)!.PolymorphismOptions.ShouldNotBeNull();
            actual.TypeDiscriminatorPropertyName.ShouldBe(expected.TypeDiscriminatorPropertyName);
            actual.IgnoreUnrecognizedTypeDiscriminators.ShouldBeFalse();
            actual.UnknownDerivedTypeHandling.ShouldBe(JsonUnknownDerivedTypeHandling.FailSerialization);
            actual.DerivedTypes.Select(static derived => (derived.DerivedType, derived.TypeDiscriminator))
                .ShouldBe(expected.DerivedTypes.Select(static derived => (derived.DerivedType, derived.TypeDiscriminator)));
        }
    }

    private static string Discriminator(JsonNode node) => node["$kind"].ShouldNotBeNull().GetValue<string>();

    private static JsonNode CodecPayload(ContentPart part) => CodecPayload(
        new UserMessage(new MessageId(Guid.NewGuid()), _agentId, _sessionId, null, _branchId, null, null, DateTimeOffset.UnixEpoch, MessageState.Complete, [part], ExtensionData.Empty));

    private static JsonNode CodecPayload(AgentMessage message)
    {
        var entry = new MessageSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(_agentId, _sessionId),
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
            _branchId,
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            message);
        var encoded = new MessageSessionEntryCodec().Encode(entry).ShouldBeOfType<SessionEntryEncoded>();
        return JsonNode.Parse(encoded.Wire.Payload.AsSpan()).ShouldNotBeNull();
    }

    private static JsonSerializerOptions SqliteOptions()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = SqliteSessionJsonTypeResolver.Create() };
        options.Converters.Add(new SqliteValueObjectJsonConverterFactory());
        return options;
    }

    private static ToolReference Tool() => new(new ToolId("search"), null, "search");

    private static ToolCallOutcome Outcome() => new(
        ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty);

    private static AssistantResponseMetadata Response() => new(
        new ModelRequestId(Guid.NewGuid()),
        new ProviderResponseIdentity(new ProviderId("test-provider"), null, new ApiFamilyId("test-api"), new ModelId("test-model"), new ModelId("test-model"), null, null, null),
        NormalizedStopReason.Completed,
        null,
        ModelUsage.NotReported,
        ExtensionData.Empty);
}
