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

    private static SessionEntryCodecCatalog Catalog() => new(
        [new MessageSessionEntryCodec()],
        TimeProvider.System);

    private static JsonSerializerOptions Options(ISessionEntryCodecCatalog catalog)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SqliteSessionEntryJsonConverterFactory(catalog));
        return options;
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
