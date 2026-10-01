// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

/// <summary>Verifies the shared built-in tool leaf observation wrapper.</summary>
public sealed class ToolLeafObservationTests
{
    private static readonly ToolCallId _callId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public async Task RunAsync_WhenTheInvocationSucceeds_EmitsAnOkActivityBoundedCounterAndLog()
    {
        var toolId = UniqueToolId();
        var logger = new RecordingLogger<ToolLeafObservationTests>();
        using var activities = Collect(toolId);
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolLeafOperationCount);

        var result = await ToolLeafObservation.RunAsync(
            toolId, _callId, logger, Events(), static () => ValueTask.FromResult(Result(ToolCallOutcomeKind.Success)));

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.GenAiOperationName).ShouldBe(AgentKitActivityNames.ExecuteTool);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("succeeded");
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(1);
        metrics.Snapshot().ShouldContain(static measurement => Equals(measurement.Tags[AgentKitTagNames.Outcome], "succeeded"));
        metrics.Snapshot().ShouldAllBe(static measurement => measurement.Tags.Keys.SequenceEqual(new[] { AgentKitTagNames.Outcome }));
    }

    [Fact]
    public async Task RunAsync_WhenTheInvocationIsRejected_ReportsRejectedWithoutAnErrorStatus()
    {
        var toolId = UniqueToolId();
        using var activities = Collect(toolId);

        _ = await ToolLeafObservation.RunAsync(
            toolId, _callId, new RecordingLogger<ToolLeafObservationTests>(), Events(), static () => ValueTask.FromResult(Result(ToolCallOutcomeKind.Rejected)));

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("rejected");
    }

    [Fact]
    public async Task RunAsync_WhenTheInvocationThrows_RecordsFaultedAndRethrowsTheSameException()
    {
        var toolId = UniqueToolId();
        var logger = new RecordingLogger<ToolLeafObservationTests>();
        using var activities = Collect(toolId);
        var failure = new InvalidOperationException("secret path /private/classified");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await ToolLeafObservation.RunAsync(toolId, _callId, logger, Events(), () => throw failure));

        thrown.ShouldBeSameAs(failure);
        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("faulted");
        span.GetTagItem(AgentKitTagNames.ErrorType).ShouldBe(nameof(InvalidOperationException));
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(3);
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), [], "/private/classified");
    }

    [Fact]
    public async Task RunAsync_WhenTheInvocationIsCancelled_RecordsCancellationAndRethrows()
    {
        var toolId = UniqueToolId();
        var logger = new RecordingLogger<ToolLeafObservationTests>();
        using var activities = Collect(toolId);

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await ToolLeafObservation.RunAsync(toolId, _callId, logger, Events(), () => throw new OperationCanceledException()));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
        logger.Snapshot().ShouldHaveSingleItem().EventId.Id.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenTheLoggerThrows_ReturnsTheUnchangedResult()
    {
        var logger = new RecordingLogger<ToolLeafObservationTests> { ThrowOnWrite = true };
        var expected = Result(ToolCallOutcomeKind.Success);

        var result = await ToolLeafObservation.RunAsync(
            UniqueToolId(), _callId, logger, Events(), () => ValueTask.FromResult(expected));

        result.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task RunAsync_WhenToolIdIsDefault_ThrowsArgumentOutOfRangeExceptionNamingToolId()
    {
        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await ToolLeafObservation.RunAsync(
                default, _callId, new RecordingLogger<ToolLeafObservationTests>(), Events(), static () => ValueTask.FromResult(Result(ToolCallOutcomeKind.Success))));

        exception.ParamName.ShouldBe("toolId");
    }

    [Fact]
    public async Task RunAsync_WhenCollaboratorsAreNull_ThrowsArgumentNullExceptionNamingParameter()
    {
        var logger = new RecordingLogger<ToolLeafObservationTests>();
        var toolId = UniqueToolId();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await ToolLeafObservation.RunAsync(toolId, _callId, null!, Events(), static () => default))).ParamName.ShouldBe("logger");
        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await ToolLeafObservation.RunAsync(toolId, _callId, logger, null!, static () => default))).ParamName.ShouldBe("events");
        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await ToolLeafObservation.RunAsync(toolId, _callId, logger, Events(), null!))).ParamName.ShouldBe("invocation");
    }

    private static ToolId UniqueToolId() => new($"leaf-observation-{Guid.NewGuid():N}");

    private static ActivityCollector Collect(ToolId toolId) =>
        new(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ExecuteTool
                && Equals(observation.GetTagItem(AgentKitTagNames.ToolId), toolId.ToString()));

    private static readonly Action<ILogger, string, Exception?> _logCompleted =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1), "completed {Outcome}");

    private static readonly Action<ILogger, Exception?> _logCancelled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(2), "cancelled");

    private static readonly Action<ILogger, string, Exception?> _logFaulted =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3), "faulted {ErrorType}");

    private static ToolLeafLogEvents Events() => new(
        static (logger, _, _, outcome) => _logCompleted(logger, outcome, null),
        static (logger, _, _) => _logCancelled(logger, null),
        static (logger, _, _, errorType) => _logFaulted(logger, errorType, null));

    private static ToolInvocationResult Result(ToolCallOutcomeKind kind) =>
        new(
            new ToolCallOutcome(
                kind,
                kind == ToolCallOutcomeKind.Success ? ToolTerminalStatus.Succeeded : ToolTerminalStatus.InvalidArguments,
                SideEffectCertainty.DefinitelyNotPerformed,
                retryable: false,
                kind == ToolCallOutcomeKind.Success ? null : "rejected",
                ExtensionData.Empty),
            [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)]);
}
