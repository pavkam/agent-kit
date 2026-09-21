// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>First-party agent loop hook point definitions registered by <see cref="HookServiceRegistration"/>.</summary>
internal static class BuiltInAgentHookPointDefinitions
{
    /// <inheritdoc cref="AgentHookPointDefinitions.RunStarted"/>
    internal static HookPointDefinition<IRunStartedHook, RunStartedEventArgs> RunStarted => AgentHookPointDefinitions.RunStarted;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeModelRequest"/>
    internal static HookPointDefinition<IBeforeModelRequestHook, BeforeModelRequestEventArgs> BeforeModelRequest =>
        AgentHookPointDefinitions.BeforeModelRequest;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeToolInvocation"/>
    internal static HookPointDefinition<IBeforeToolInvocationHook, BeforeToolInvocationEventArgs> BeforeToolInvocation =>
        AgentHookPointDefinitions.BeforeToolInvocation;

    /// <inheritdoc cref="AgentHookPointDefinitions.RunStartedRegistration"/>
    internal static HookPointDefinitionRegistration RunStartedRegistration => AgentHookPointDefinitions.RunStartedRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeModelRequestRegistration"/>
    internal static HookPointDefinitionRegistration BeforeModelRequestRegistration =>
        AgentHookPointDefinitions.BeforeModelRequestRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeToolInvocationRegistration"/>
    internal static HookPointDefinitionRegistration BeforeToolInvocationRegistration =>
        AgentHookPointDefinitions.BeforeToolInvocationRegistration;
}
