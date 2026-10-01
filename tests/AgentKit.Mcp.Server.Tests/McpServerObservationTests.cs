// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server.Tests;

/// <summary>Verifies MCP server request and tool-call observation without leaking protected content.</summary>
public sealed class McpServerObservationTests
{
    [Fact]
    public async Task ObserveToolCallAsync_WhenTheCallSucceeds_EmitsAnOkActivityLogAndBoundedCounter()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var logger = new RecordingLogger<McpServerObservation>();
        var observation = new McpServerObservation(logger);
        using var activities = Collect(AgentKitActivityNames.McpServerToolCall, serverKey);
        using var metrics = new MetricCollector(AgentKitMetricNames.McpServerOperationCount);

        var result = await observation.ObserveToolCallAsync(
            serverKey,
            "weather.get",
            static () => ValueTask.FromResult(new CallToolResult()),
            McpServerObservation.ClassifyToolResult);

        result.IsError.ShouldNotBe(true);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.ToolName).ShouldBe("weather.get");
        span.GetTagItem(AgentKitTagNames.McpOperation).ShouldBe("tools.call");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("succeeded");
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(13210);
        entry.Level.ShouldBe(LogLevel.Debug);
        metrics.Snapshot().ShouldContain(static measurement => measurement.Tags[AgentKitTagNames.Outcome]!.Equals("succeeded"));
        metrics.Snapshot().ShouldAllBe(static measurement => measurement.Tags.Keys.Order().SequenceEqual(
            new[] { AgentKitTagNames.McpOperation, AgentKitTagNames.Outcome }.Order()));
    }

    [Fact]
    public async Task ObserveToolCallAsync_WhenTheToolReportsAnError_MarksTheActivityErroredAndKeepsTheResult()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var observation = new McpServerObservation(new RecordingLogger<McpServerObservation>());
        using var activities = Collect(AgentKitActivityNames.McpServerToolCall, serverKey);

        var result = await observation.ObserveToolCallAsync(
            serverKey,
            "failing.tool",
            static () => ValueTask.FromResult(new CallToolResult { IsError = true }),
            McpServerObservation.ClassifyToolResult);

        result.IsError.ShouldBe(true);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("error");
    }

    [Fact]
    public async Task ObserveToolCallAsync_WhenTheToolNameIsUnbounded_TruncatesItInEverySignal()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var logger = new RecordingLogger<McpServerObservation>();
        var observation = new McpServerObservation(logger);
        using var activities = Collect(AgentKitActivityNames.McpServerToolCall, serverKey);
        var longName = new string('x', McpServerObservation.MaximumToolNameLength * 4);

        _ = await observation.ObserveToolCallAsync(
            serverKey, longName, static () => ValueTask.FromResult(new CallToolResult()), McpServerObservation.ClassifyToolResult);

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.ToolName)
            .ShouldBe(new string('x', McpServerObservation.MaximumToolNameLength));
        logger.Snapshot().ShouldHaveSingleItem().Message.ShouldNotContain(longName);
    }

    [Fact]
    public async Task ObserveRequestAsync_WhenTheActionThrows_RecordsTheFailureAndRethrowsTheSameException()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var logger = new RecordingLogger<McpServerObservation>();
        var observation = new McpServerObservation(logger);
        using var activities = Collect(AgentKitActivityNames.McpServerRequest, serverKey);
        var failure = new InvalidOperationException("secret peer detail");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await observation.ObserveRequestAsync<McpResponse>(serverKey, "tools.call", () => throw failure, McpServerObservation.Classify));

        thrown.ShouldBeSameAs(failure);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("faulted");
        span.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(nameof(InvalidOperationException));
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(13202);
        entry.Level.ShouldBe(LogLevel.Error);
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), [], "secret peer detail");
    }

    [Fact]
    public async Task ObserveToolCallAsync_WhenTheCallIsCancelled_RecordsCancellationAndRethrows()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var logger = new RecordingLogger<McpServerObservation>();
        var observation = new McpServerObservation(logger);
        using var activities = Collect(AgentKitActivityNames.McpServerToolCall, serverKey);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await observation.ObserveToolCallAsync<CallToolResult>(
                serverKey, "slow.tool", () => throw new OperationCanceledException(), McpServerObservation.ClassifyToolResult));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(13211);
    }

    [Fact]
    public async Task ObserveRequestAsync_WhenTheLoggerThrows_ReturnsTheUnchangedResponse()
    {
        var serverKey = $"server-{Guid.NewGuid():N}";
        var logger = new RecordingLogger<McpServerObservation> { ThrowOnWrite = true };
        var observation = new McpServerObservation(logger);
        var requestId = new McpRequestId(Guid.NewGuid());

        var response = await observation.ObserveRequestAsync(
            serverKey,
            "tools.call",
            () => ValueTask.FromResult<McpResponse>(new McpResponseDenied(requestId, "denied")),
            McpServerObservation.Classify);

        response.ShouldBeOfType<McpResponseDenied>().RequestId.ShouldBe(requestId);
    }

    [Fact]
    public void Classify_WhenGivenEveryResponseKind_ReturnsABoundedOutcome()
    {
        var id = new McpRequestId(Guid.NewGuid());
        var outcomes = new McpResponse[]
        {
            new McpResponseDenied(id, "denied"),
            new McpResponseUnsupportedCapability(id, "x"),
        }.Select(McpServerObservation.Classify);

        outcomes.ShouldBe(["denied", "unsupported"]);
    }

    private static ActivityCollector Collect(string operationName, string serverKey) =>
        new(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == operationName
                && Equals(observation.GetTagItem(AgentKitTagNames.McpServerKey), serverKey));
}
