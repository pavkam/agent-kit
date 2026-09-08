// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

/// <summary>Verifies retained committed-tool evidence is exact and ordered.</summary>
public sealed class CommittedTurnContinuationBoundaryTests
{
    [Fact]
    public void Constructor_WhenToolReferenceIsMissing_ThrowsExactArgumentException()
    {
        var response = CreateResponse(NewCallId(1), NewCallId(2));

        var exception = Should.Throw<ArgumentException>(() =>
            new CommittedTurnContinuationBoundary(response, [Reference(1, response.TurnId!.Value)], null, false));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("toolResults");
    }

    [Fact]
    public void Constructor_WhenToolReferencesAreReordered_ThrowsExactArgumentException()
    {
        var response = CreateResponse(NewCallId(1), NewCallId(2));

        var exception = Should.Throw<ArgumentException>(() => new CommittedTurnContinuationBoundary(
            response,
            [Reference(2, response.TurnId!.Value), Reference(1, response.TurnId.Value)],
            null,
            false));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("toolResults");
    }

    [Fact]
    public void Constructor_WhenResponseRepeatsToolCall_ThrowsExactArgumentException()
    {
        var callId = NewCallId(1);
        var response = CreateResponse(callId, callId);

        var exception = Should.Throw<ArgumentException>(() => new CommittedTurnContinuationBoundary(
            response, [Reference(1, response.TurnId!.Value), Reference(2, response.TurnId.Value, callId)], null, false));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("toolResults");
    }

    [Fact]
    public void Constructor_WhenResponseTurnIdIsDefault_ThrowsExactArgumentException()
    {
        var response = CreateResponse(NewCallId(1)) with { TurnId = default(TurnId) };

        var exception = Should.Throw<ArgumentException>(() =>
            new CommittedTurnContinuationBoundary(response, [], null, false));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("toolResults");
    }

    private static ToolCallId NewCallId(int value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:D12}"));

    private static CommittedToolResultReference Reference(
        int value,
        TurnId turnId,
        ToolCallId? callId = null) => new(
        new SessionEntryId(Guid.Parse($"10000000-0000-0000-0000-{value:D12}")),
        callId ?? NewCallId(value),
        turnId);

    private static AssistantMessage CreateResponse(params ToolCallId[] callIds)
    {
        var requestId = new ModelRequestId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var parts = callIds.Select(static callId => (ContentPart) new ToolCallPart(
            callId,
            new ToolReference(new ToolId("tool"), null, "tool"),
            default,
            null,
            ExtensionData.Empty)).ToImmutableArray();
        return new AssistantMessage(
            new MessageId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000001")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
            null,
            new BranchId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
            new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")),
            new TurnId(Guid.Parse("80000000-0000-0000-0000-000000000001")),
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            parts,
            new AssistantResponseMetadata(
                requestId,
                new ProviderResponseIdentity(
                    new ProviderId("test"),
                    null,
                    new ApiFamilyId("test"),
                    new ModelId("test"),
                    new ModelId("test"),
                    null,
                    null,
                    null),
                NormalizedStopReason.ToolUse,
                null,
                ModelUsage.Empty,
                ExtensionData.Empty),
            ExtensionData.Empty);
    }
}
