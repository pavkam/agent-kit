// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

/// <summary>Verifies <see cref="HookPointDefinitionRegistrations"/> behavior and contracts.</summary>
public sealed class HookPointDefinitionRegistrationsTests
{
    [Fact]
    public void ToDictionary_WhenPointDefinitionsCollide_ThrowsHookCompositionException()
    {
        var registrations = new HookPointDefinitionRegistration[]
        {
            AgentHookPointDefinitions.RunStartedRegistration,
            new HookPointDefinitionRegistration(
                AgentHookPoints.RunStarted,
                typeof(IBeforeModelRequestHook),
                typeof(BeforeModelRequestEventArgs),
                HookPointKind.Mutating,
                HookFailureMode.FailOperation),
        };

        var exception = Should.Throw<HookCompositionException>(() => HookPointDefinitionRegistrations.ToDictionary(registrations));

        exception.Message.ShouldContain("incompatible closed types");
    }
}
