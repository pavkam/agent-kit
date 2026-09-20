// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Closed hook point definitions and registrations for the first-party agent loop points.</summary>
internal static class BuiltInAgentHookPointDefinitions
{
    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.RunStarted"/>.</summary>
    public static HookPointDefinition<IRunStartedHook, RunStartedEventArgs> RunStarted { get; } = new(
        AgentHookPoints.RunStarted,
        HookPointKind.Observational,
        HookFailureMode.IsolateAndDiagnose,
        new DefaultAgentHookMutationValidator<RunStartedEventArgs>(),
        static (hook, args, _, cancellationToken) => hook.OnRunStartedAsync(args, cancellationToken));

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeModelRequest"/>.</summary>
    public static HookPointDefinition<IBeforeModelRequestHook, BeforeModelRequestEventArgs> BeforeModelRequest { get; } = new(
        AgentHookPoints.BeforeModelRequest,
        HookPointKind.Mutating,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeModelRequestEventArgs>(),
        static (hook, args, _, cancellationToken) => hook.OnBeforeModelRequestAsync(args, cancellationToken));

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeToolInvocation"/>.</summary>
    public static HookPointDefinition<IBeforeToolInvocationHook, BeforeToolInvocationEventArgs> BeforeToolInvocation { get; } = new(
        AgentHookPoints.BeforeToolInvocation,
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeToolInvocationEventArgs>(),
        static (hook, args, _, cancellationToken) => hook.OnBeforeToolInvocationAsync(args, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.RunStarted"/>.</summary>
    public static HookPointDefinitionRegistration RunStartedRegistration { get; } = new(
        AgentHookPoints.RunStarted,
        typeof(IRunStartedHook),
        typeof(RunStartedEventArgs),
        HookPointKind.Observational,
        HookFailureMode.IsolateAndDiagnose);

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.BeforeModelRequest"/>.</summary>
    public static HookPointDefinitionRegistration BeforeModelRequestRegistration { get; } = new(
        AgentHookPoints.BeforeModelRequest,
        typeof(IBeforeModelRequestHook),
        typeof(BeforeModelRequestEventArgs),
        HookPointKind.Mutating,
        HookFailureMode.FailOperation);

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.BeforeToolInvocation"/>.</summary>
    public static HookPointDefinitionRegistration BeforeToolInvocationRegistration { get; } = new(
        AgentHookPoints.BeforeToolInvocation,
        typeof(IBeforeToolInvocationHook),
        typeof(BeforeToolInvocationEventArgs),
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation);
}
