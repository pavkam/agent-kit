// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Tests;

/// <summary>Behavioral tests for <see cref="DefaultHistoryPipeline"/>.</summary>
public sealed class DefaultHistoryPipelineTests
{
    private readonly DefaultHistoryPipeline _pipeline = new();

    [Fact]
    public async Task PrepareAsync_WhenHistoryIsEmpty_ReturnsRejectedHistoryWithEmptyHistory()
    {
        var cursor = TestFactory.MessageCursor();
        var result = await _pipeline.PrepareAsync(new HistoryPreparationRequest(cursor, []), TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<RejectedHistory>();
        rejected.Failure.Kind.ShouldBe(HistoryFailureKind.EmptyHistory);
    }

    [Fact]
    public async Task PrepareAsync_WhenHistoryIsValid_ReturnsPreparedHistoryWithRepairs()
    {
        var system = TestFactory.SystemMessage("hidden");
        var user = TestFactory.UserMessage();
        var cursor = new MessageCursor(
            user.AgentId,
            user.SessionId,
            user.ConversationId,
            user.BranchId,
            new SessionVersion(1),
            new SessionSequence(0));
        var result = await _pipeline.PrepareAsync(
            new HistoryPreparationRequest(cursor, [system, user]),
            TestContext.Current.CancellationToken);

        var prepared = result.ShouldBeOfType<PreparedHistory>();
        prepared.View.Messages.ShouldBe([user]);
        prepared.View.Repairs.Length.ShouldBe(1);
        prepared.View.Repairs[0].Kind.ShouldBe(HistoryRepairKind.ExcludedInstructionMessage);
    }
}
