// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookRegistrationDescriptorTests
{
    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookRegistrationDescriptor(
            default, HookKernelTestData.Point, HookKernelTestData.Profile, HookOrder.Normal,
            HookLifetime.Singleton, HookFailureMode.FailOperation, HookReentrancyPolicy.Forbidden, [], [], []));

        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenPointIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookRegistrationDescriptor(
            new HookRegistrationId(Guid.NewGuid()), default, HookKernelTestData.Profile, HookOrder.Normal,
            HookLifetime.Singleton, HookFailureMode.FailOperation, HookReentrancyPolicy.Forbidden, [], [], []));

        exception.ParamName.ShouldBe("point");
    }

    [Fact]
    public void Constructor_WhenProfileKeyIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookRegistrationDescriptor(
            new HookRegistrationId(Guid.NewGuid()), HookKernelTestData.Point, default, HookOrder.Normal,
            HookLifetime.Singleton, HookFailureMode.FailOperation, HookReentrancyPolicy.Forbidden, [], [], []));

        exception.ParamName.ShouldBe("profileKey");
    }

    [Fact]
    public void Constructor_WhenOrderIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookRegistrationDescriptor(
            new HookRegistrationId(Guid.NewGuid()), HookKernelTestData.Point, HookKernelTestData.Profile, null!,
            HookLifetime.Singleton, HookFailureMode.FailOperation, HookReentrancyPolicy.Forbidden, [], [], []));

        exception.ParamName.ShouldBe("order");
    }

    [Fact]
    public void Constructor_WhenLifetimeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookRegistrationDescriptor(
            new HookRegistrationId(Guid.NewGuid()), HookKernelTestData.Point, HookKernelTestData.Profile, HookOrder.Normal,
            (HookLifetime) 99, HookFailureMode.FailOperation, HookReentrancyPolicy.Forbidden, [], [], []));

        exception.ParamName.ShouldBe("lifetime");
    }

    [Fact]
    public void Constructor_WhenRequestedFailureModeIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookRegistrationDescriptor(
            new HookRegistrationId(Guid.NewGuid()), HookKernelTestData.Point, HookKernelTestData.Profile, HookOrder.Normal,
            HookLifetime.Singleton, (HookFailureMode) 99, HookReentrancyPolicy.Forbidden, [], [], []));

        exception.ParamName.ShouldBe("requestedFailureMode");
    }

    [Fact]
    public void Constructor_WhenReentrancyIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookRegistrationDescriptor(
            new HookRegistrationId(Guid.NewGuid()), HookKernelTestData.Point, HookKernelTestData.Profile, HookOrder.Normal,
            HookLifetime.Singleton, HookFailureMode.FailOperation, (HookReentrancyPolicy) 99, [], [], []));

        exception.ParamName.ShouldBe("reentrancy");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var id = new HookRegistrationId(Guid.NewGuid());
        var dependsOn = new HookRegistrationId(Guid.NewGuid());

        var registration = new HookRegistrationDescriptor(
            id, HookKernelTestData.Point, HookKernelTestData.Profile, HookOrder.First,
            HookLifetime.Scoped, HookFailureMode.FailOperation, HookReentrancyPolicy.Bounded,
            [], [], [dependsOn]);

        registration.Id.ShouldBe(id);
        registration.Point.ShouldBe(HookKernelTestData.Point);
        registration.ProfileKey.ShouldBe(HookKernelTestData.Profile);
        registration.Order.ShouldBe(HookOrder.First);
        registration.Lifetime.ShouldBe(HookLifetime.Scoped);
        registration.RequestedFailureMode.ShouldBe(HookFailureMode.FailOperation);
        registration.Reentrancy.ShouldBe(HookReentrancyPolicy.Bounded);
        registration.DependsOn.ShouldBe([dependsOn]);
    }

    [Fact]
    public void Equality_WhenEveryFieldMatches_InstancesAreStructurallyEqual()
    {
        var id = new HookRegistrationId(Guid.NewGuid());

        var first = new HookRegistrationDescriptor(
            id, HookKernelTestData.Point, HookKernelTestData.Profile, HookOrder.Normal,
            HookLifetime.Singleton, HookFailureMode.FailOperation, HookReentrancyPolicy.Forbidden, [], [], []);
        var second = new HookRegistrationDescriptor(
            id, HookKernelTestData.Point, HookKernelTestData.Profile, HookOrder.Normal,
            HookLifetime.Singleton, HookFailureMode.FailOperation, HookReentrancyPolicy.Forbidden, [], [], []);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
