// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies ToolBatch behavior and contracts.</summary>
public sealed class ToolBatchTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        ImmutableArray<ToolBatchEntry> entries = [BatchEntry(0)];
        var deadline = DateTimeOffset.UnixEpoch.AddMinutes(1);
        var batch = new ToolBatch(TestAgentId, TestSessionId, TestRunId, entries, ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, deadline);
        batch.AgentId.ShouldBe(TestAgentId);
        batch.SessionId.ShouldBe(TestSessionId);
        batch.RunId.ShouldBe(TestRunId);
        batch.Entries.ShouldBe(entries);
        batch.FailureMode.ShouldBe(ToolBatchFailureMode.SettleIndependently);
        batch.UnknownSchedulingMode.ShouldBe(UnknownSchedulingMode.Sequential);
        batch.Deadline.ShouldBe(deadline);
    }

    [Fact]
    public void Constructor_WhenEntriesIsEmpty_Succeeds()
    {
        var batch = new ToolBatch(TestAgentId, TestSessionId, TestRunId, [], ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1));
        batch.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenAgentIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolBatch(default, TestSessionId, TestRunId, [], ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("agentId");

    [Fact]
    public void Constructor_WhenSessionIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolBatch(TestAgentId, default, TestRunId, [], ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("sessionId");

    [Fact]
    public void Constructor_WhenRunIdIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolBatch(TestAgentId, TestSessionId, default, [], ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("runId");

    [Fact]
    public void Constructor_WhenEntriesIsUninitialized_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolBatch(TestAgentId, TestSessionId, TestRunId, default, ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("entries");

    [Fact]
    public void Constructor_WhenEntriesContainsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolBatch(TestAgentId, TestSessionId, TestRunId, [null!], ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("entries");

    [Fact]
    public void Constructor_WhenEntriesHaveDuplicateCallId_ThrowsExactParameter()
    {
        ImmutableArray<ToolBatchEntry> entries = [BatchEntry(0), BatchEntry(1)];
        var exception = Should.Throw<ArgumentException>(() => new ToolBatch(TestAgentId, TestSessionId, TestRunId, entries, ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1)));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void Constructor_WhenEntryAgentDoesNotMatch_ThrowsExactParameter()
    {
        var otherAgentId = new AgentId(Guid.NewGuid());
        ImmutableArray<ToolBatchEntry> entries = [BatchEntry(0)];
        var exception = Should.Throw<ArgumentException>(() => new ToolBatch(otherAgentId, TestSessionId, TestRunId, entries, ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1)));
        exception.ParamName.ShouldBe("entries");
    }

    [Fact]
    public void Constructor_WhenFailureModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolBatch(TestAgentId, TestSessionId, TestRunId, [], (ToolBatchFailureMode) 99, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("failureMode");

    [Fact]
    public void Constructor_WhenUnknownSchedulingModeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolBatch(TestAgentId, TestSessionId, TestRunId, [], ToolBatchFailureMode.SettleIndependently, (UnknownSchedulingMode) 99, DateTimeOffset.UnixEpoch.AddMinutes(1))).ParamName.ShouldBe("unknownSchedulingMode");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolBatch(TestAgentId, TestSessionId, TestRunId, [BatchEntry(0)], ToolBatchFailureMode.SettleIndependently, UnknownSchedulingMode.Sequential, DateTimeOffset.UnixEpoch.AddMinutes(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
