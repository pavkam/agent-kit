// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookRegistrationSnapshotTests
{
    [Fact]
    public void Constructor_WhenRegistrationsContainsNull_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new HookRegistrationSnapshot([null!]));
        exception.ParamName.ShouldBe("registrations");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsRegistrations()
    {
        var registration = HookKernelTestData.Registration();
        new HookRegistrationSnapshot([registration]).Registrations.ShouldBe([registration]);
    }

    [Fact]
    public void Equality_WhenRegistrationsMatchByValue_InstancesAreStructurallyEqual()
    {
        var registration = HookKernelTestData.Registration();

        var first = new HookRegistrationSnapshot([registration]);
        var second = new HookRegistrationSnapshot([registration]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
