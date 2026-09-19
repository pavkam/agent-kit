// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookInvocationContextTests
{
    [Fact]
    public void Constructor_WhenRegistrationIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationContext(
            default, new HookInvocationId(Guid.NewGuid()), new HookDispatchId(Guid.NewGuid()), 1));

        exception.ParamName.ShouldBe("registrationId");
    }

    [Fact]
    public void Constructor_WhenInvocationIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationContext(
            new HookRegistrationId(Guid.NewGuid()), default, new HookDispatchId(Guid.NewGuid()), 1));

        exception.ParamName.ShouldBe("invocationId");
    }

    [Fact]
    public void Constructor_WhenDispatchIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationContext(
            new HookRegistrationId(Guid.NewGuid()), new HookInvocationId(Guid.NewGuid()), default, 1));

        exception.ParamName.ShouldBe("dispatchId");
    }

    [Fact]
    public void Constructor_WhenDepthIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookInvocationContext(
            new HookRegistrationId(Guid.NewGuid()), new HookInvocationId(Guid.NewGuid()), new HookDispatchId(Guid.NewGuid()), -1));

        exception.ParamName.ShouldBe("depth");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var registrationId = new HookRegistrationId(Guid.NewGuid());
        var invocationId = new HookInvocationId(Guid.NewGuid());
        var dispatchId = new HookDispatchId(Guid.NewGuid());

        var context = new HookInvocationContext(registrationId, invocationId, dispatchId, 2);

        context.RegistrationId.ShouldBe(registrationId);
        context.InvocationId.ShouldBe(invocationId);
        context.DispatchId.ShouldBe(dispatchId);
        context.Depth.ShouldBe(2);
    }
}
