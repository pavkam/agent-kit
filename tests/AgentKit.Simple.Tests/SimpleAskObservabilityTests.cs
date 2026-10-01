// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies the isolation guarantees of the Simple ask observation wrapper.</summary>
public sealed class SimpleAskObservabilityTests
{
    [Fact]
    public async Task RunAsync_WhenTheLoggerThrows_ReturnsTheUnchangedResult()
    {
        var logger = new RecordingLogger<SimpleAskObservabilityTests> { ThrowOnWrite = true };

        var result = await SimpleAskObservability.RunAsync(logger, agentId: null, sessionId: null, static () => Task.FromResult("reply"));

        result.ShouldBe("reply");
    }

    [Fact]
    public async Task RunAsync_WhenTheActionThrows_RethrowsTheSameExceptionAndLogsTheTypeOnly()
    {
        var logger = new RecordingLogger<SimpleAskObservabilityTests>();
        var failure = new InvalidOperationException("secret detail");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await SimpleAskObservability.RunAsync(logger, null, null, () => throw failure));

        thrown.ShouldBeSameAs(failure);
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(27001);
        entry.Message.ShouldNotContain("secret detail");
    }

    [Fact]
    public async Task RunAsync_WhenTheActionIsCancelled_LogsCancellationAndRethrows()
    {
        var logger = new RecordingLogger<SimpleAskObservabilityTests>();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await SimpleAskObservability.RunAsync(logger, null, null, () => throw new OperationCanceledException()));

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(27000);
        entry.State["Outcome"].ShouldBe("cancelled");
    }

    [Fact]
    public async Task RunAsync_WhenCollaboratorsAreNull_ThrowsArgumentNullExceptionNamingParameter()
    {
        (await Should.ThrowAsync<ArgumentNullException>(
            async () => await SimpleAskObservability.RunAsync(null!, null, null, static () => Task.FromResult("x")))).ParamName.ShouldBe("logger");
        (await Should.ThrowAsync<ArgumentNullException>(
            async () => await SimpleAskObservability.RunAsync(new RecordingLogger<SimpleAskObservabilityTests>(), null, null, null!))).ParamName.ShouldBe("action");
    }
}
