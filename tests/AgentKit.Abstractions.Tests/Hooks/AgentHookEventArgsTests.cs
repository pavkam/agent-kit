// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class AgentHookEventArgsTests
{
    [Fact]
    public void Constructor_WhenCorrelationNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new TestEventArgs(
            null!, DateTimeOffset.UnixEpoch, new HookInvocationId(Guid.NewGuid())));

        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void Constructor_WhenValid_ExposesEveryProperty()
    {
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null);
        var timestamp = DateTimeOffset.UnixEpoch;
        var invocationId = new HookInvocationId(Guid.NewGuid());

        var args = new TestEventArgs(correlation, timestamp, invocationId);

        args.Correlation.ShouldBe(correlation);
        args.Timestamp.ShouldBe(timestamp);
        args.InvocationId.ShouldBe(invocationId);
    }

    [Fact]
    public void Validate_WhenNotOverridden_DoesNotThrow()
    {
        var args = new TestEventArgs(
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
            DateTimeOffset.UnixEpoch,
            new HookInvocationId(Guid.NewGuid()));

        Should.NotThrow(args.Validate);
    }

    [Fact]
    public void CaptureMutableState_WhenNotOverridden_ReturnsNull()
    {
        var args = new TestEventArgs(
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
            DateTimeOffset.UnixEpoch,
            new HookInvocationId(Guid.NewGuid()));

        args.CaptureMutableState().ShouldBeNull();
    }

    [Fact]
    public void RestoreMutableState_WhenNotOverridden_DoesNotThrow()
    {
        var args = new TestEventArgs(
            new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), new RunId(Guid.NewGuid()), null),
            DateTimeOffset.UnixEpoch,
            new HookInvocationId(Guid.NewGuid()));

        Should.NotThrow(() => args.RestoreMutableState(null));
    }

    private sealed class TestEventArgs: AgentHookEventArgs
    {
        public TestEventArgs(
            OperationCorrelation correlation,
            DateTimeOffset timestamp,
            HookInvocationId invocationId)
            : base(correlation, timestamp, invocationId)
        {
        }
    }
}
