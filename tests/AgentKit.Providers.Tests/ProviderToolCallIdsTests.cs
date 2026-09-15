// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using System.Text.Json;

/// <summary>Verifies how <see cref="ProviderToolCallIds"/> maps conversation tool calls to their wire identifiers.</summary>
public sealed class ProviderToolCallIdsTests
{
    private static readonly AgentId Agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId Session = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static readonly BranchId Branch = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset CreatedAt = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ToolCallPart ToolCall(ToolCallId callId, string? providerCallId)
    {
        using var document = JsonDocument.Parse("{}");
        return new ToolCallPart(
            callId,
            new ToolReference(new ToolId("get_weather"), null, "get_weather"),
            document.RootElement.Clone(),
            providerCallId is null ? null : new ProviderToolCallId(providerCallId),
            ExtensionData.Empty);
    }

    private static AssistantMessage Assistant(params ContentPart[] parts) =>
        new(
            new MessageId(Guid.NewGuid()),
            Agent,
            Session,
            conversationId: null,
            Branch,
            runId: null,
            turnId: null,
            CreatedAt,
            MessageState.Complete,
            [.. parts],
            new AssistantResponseMetadata(
                new ModelRequestId(Guid.NewGuid()),
                new ProviderResponseIdentity(
                    new ProviderId("test"),
                    upstreamProviderId: null,
                    new ApiFamilyId("test-chat"),
                    new ModelId("m"),
                    new ModelId("m"),
                    deploymentId: null,
                    requestId: null,
                    responseId: null),
                NormalizedStopReason.ToolUse,
                rawStopReason: null,
                ModelUsage.NotReported,
                ExtensionData.Empty),
            ExtensionData.Empty);

    private static UserMessage User(string text) =>
        new(
            new MessageId(Guid.NewGuid()),
            Agent,
            Session,
            conversationId: null,
            Branch,
            runId: null,
            turnId: null,
            CreatedAt,
            MessageState.Complete,
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);

    [Fact]
    public void Collect_WhenMessagesIsDefault_ReturnsEmptyMap()
    {
        var map = ProviderToolCallIds.Collect(default);

        map.ShouldBeEmpty();
    }

    [Fact]
    public void Collect_WhenNoToolCalls_ReturnsEmptyMap()
    {
        var map = ProviderToolCallIds.Collect([User("hi"), Assistant(new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty))]);

        map.ShouldBeEmpty();
    }

    [Fact]
    public void Collect_WhenToolCallHasProviderCallId_MapsToProviderValue()
    {
        var callId = new ToolCallId(Guid.NewGuid());

        var map = ProviderToolCallIds.Collect([Assistant(ToolCall(callId, "call_abc"))]);

        _ = map.ShouldHaveSingleItem();
        map[callId].ShouldBe("call_abc");
    }

    [Fact]
    public void Collect_WhenToolCallHasNoProviderCallId_MapsToCanonicalToolCallIdText()
    {
        var callId = new ToolCallId(Guid.Parse("7f000000-0000-4000-8000-000000000001"));

        var map = ProviderToolCallIds.Collect([Assistant(ToolCall(callId, null))]);

        map[callId].ShouldBe("7f000000-0000-4000-8000-000000000001");
    }

    [Fact]
    public void Collect_WhenSameCallIdAppearsTwice_LastOccurrenceWins()
    {
        var callId = new ToolCallId(Guid.NewGuid());

        var map = ProviderToolCallIds.Collect([Assistant(ToolCall(callId, "first")), Assistant(ToolCall(callId, "second"))]);

        _ = map.ShouldHaveSingleItem();
        map[callId].ShouldBe("second");
    }

    [Fact]
    public void Collect_WhenSeveralToolCallsAcrossMessages_MapsEachIndependently()
    {
        var first = new ToolCallId(Guid.NewGuid());
        var second = new ToolCallId(Guid.NewGuid());

        var map = ProviderToolCallIds.Collect([Assistant(ToolCall(first, "a"), ToolCall(second, null)), User("ok")]);

        map.Count.ShouldBe(2);
        map[first].ShouldBe("a");
        map[second].ShouldBe(second.ToString());
    }
}
