// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

public sealed class HookPointDefinitionRegistrationTests
{
    [Fact]
    public void Constructor_WhenValid_CreatesRegistration()
    {
        var registration = new HookPointDefinitionRegistration(
            AgentHookPoints.RunStarted,
            typeof(IRunStartedHook),
            typeof(RunStartedEventArgs),
            HookPointKind.Observational,
            HookFailureMode.IsolateAndDiagnose);

        registration.Point.ShouldBe(AgentHookPoints.RunStarted);
        registration.HookInterface.ShouldBe(typeof(IRunStartedHook));
        registration.EventArgsType.ShouldBe(typeof(RunStartedEventArgs));
        registration.Kind.ShouldBe(HookPointKind.Observational);
        registration.FailureInvariant.ShouldBe(HookFailureMode.IsolateAndDiagnose);
    }

    [Fact]
    public void Constructor_WhenMutatingPointUsesIsolation_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new HookPointDefinitionRegistration(
            AgentHookPoints.BeforeModelRequest,
            typeof(IBeforeModelRequestHook),
            typeof(BeforeModelRequestEventArgs),
            HookPointKind.Mutating,
            HookFailureMode.IsolateAndDiagnose));
    }
}
