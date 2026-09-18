// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

/// <summary>Verifies <see cref="RunStartedEventArgs"/> argument checks and read-only exposure.</summary>
public sealed class RunStartedEventArgsTests
{
    private static readonly BranchId _branch = new(Guid.NewGuid());

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesRunIdentitiesAndBounds()
    {
        var invocation = new HookInvocationId(Guid.NewGuid());

        var args = new RunStartedEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.RunCorrelation,
            DateTimeOffset.UnixEpoch, invocation, _branch, HookPointEventArgsTestData.Model, 4, TimeSpan.FromSeconds(30));

        args.RunId.ShouldBe(HookPointEventArgsTestData.RunCorrelation.RunId);
        args.BranchId.ShouldBe(_branch);
        args.Model.ShouldBeSameAs(HookPointEventArgsTestData.Model);
        args.MaxTurns.ShouldBe(4);
        args.AttemptTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        args.SessionId.ShouldBe(HookPointEventArgsTestData.SessionId);
        args.InvocationId.ShouldBe(invocation);
        args.CaptureMutableState().ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenBranchIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunStartedEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.RunCorrelation,
            DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), default, HookPointEventArgsTestData.Model, 4, TimeSpan.FromSeconds(30)))
            .ParamName.ShouldBe("branchId");

    [Fact]
    public void Constructor_WhenModelIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RunStartedEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.RunCorrelation,
            DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), _branch, null!, 4, TimeSpan.FromSeconds(30)))
            .ParamName.ShouldBe("model");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxTurnsIsNotPositive_ThrowsArgumentOutOfRangeException(int maxTurns) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunStartedEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.RunCorrelation,
            DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), _branch, HookPointEventArgsTestData.Model, maxTurns, TimeSpan.FromSeconds(30)))
            .ParamName.ShouldBe("maxTurns");

    [Fact]
    public void Constructor_WhenAttemptTimeoutIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunStartedEventArgs(
            HookPointEventArgsTestData.AgentId, HookPointEventArgsTestData.SessionId, HookPointEventArgsTestData.RunCorrelation,
            DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid()), _branch, HookPointEventArgsTestData.Model, 4, TimeSpan.Zero))
            .ParamName.ShouldBe("attemptTimeout");

    [Fact]
    public void AgentHookPoints_WhenRead_AreDistinctStableIdentities()
    {
        AgentHookPoints.RunStarted.Value.ShouldBe("agentkit.run.started");
        AgentHookPoints.BeforeModelRequest.Value.ShouldBe("agentkit.model.request.before");
        AgentHookPoints.BeforeToolInvocation.Value.ShouldBe("agentkit.tool.invocation.before");
        new[] { AgentHookPoints.RunStarted, AgentHookPoints.BeforeModelRequest, AgentHookPoints.BeforeToolInvocation }.Distinct().Count().ShouldBe(3);
    }
}
