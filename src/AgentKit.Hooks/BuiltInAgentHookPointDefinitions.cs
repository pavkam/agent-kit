// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>First-party agent loop hook point definitions registered by <see cref="HookServiceRegistration"/>.</summary>
internal static class BuiltInAgentHookPointDefinitions
{
    /// <inheritdoc cref="AgentHookPointDefinitions.RunStarted"/>
    internal static HookPointDefinition<IRunStartedHook, RunStartedEventArgs> RunStarted => AgentHookPointDefinitions.RunStarted;

    /// <inheritdoc cref="AgentHookPointDefinitions.ContextAssembled"/>
    internal static HookPointDefinition<IContextAssembledHook, ContextAssembledEventArgs> ContextAssembled =>
        AgentHookPointDefinitions.ContextAssembled;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeModelRequest"/>
    internal static HookPointDefinition<IBeforeModelRequestHook, BeforeModelRequestEventArgs> BeforeModelRequest =>
        AgentHookPointDefinitions.BeforeModelRequest;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeToolInvocation"/>
    internal static HookPointDefinition<IBeforeToolInvocationHook, BeforeToolInvocationEventArgs> BeforeToolInvocation =>
        AgentHookPointDefinitions.BeforeToolInvocation;

    /// <inheritdoc cref="AgentHookPointDefinitions.ToolResult"/>
    internal static HookPointDefinition<IToolResultHook, ToolResultHookEventArgs> ToolResult =>
        AgentHookPointDefinitions.ToolResult;

    /// <inheritdoc cref="AgentHookPointDefinitions.OutputValidating"/>
    internal static HookPointDefinition<IOutputValidatingHook, OutputValidatingEventArgs> OutputValidating =>
        AgentHookPointDefinitions.OutputValidating;

    /// <inheritdoc cref="AgentHookPointDefinitions.RunStartedRegistration"/>
    internal static HookPointDefinitionRegistration RunStartedRegistration => AgentHookPointDefinitions.RunStartedRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.ContextAssembledRegistration"/>
    internal static HookPointDefinitionRegistration ContextAssembledRegistration =>
        AgentHookPointDefinitions.ContextAssembledRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeModelRequestRegistration"/>
    internal static HookPointDefinitionRegistration BeforeModelRequestRegistration =>
        AgentHookPointDefinitions.BeforeModelRequestRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeToolInvocationRegistration"/>
    internal static HookPointDefinitionRegistration BeforeToolInvocationRegistration =>
        AgentHookPointDefinitions.BeforeToolInvocationRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.ToolResultRegistration"/>
    internal static HookPointDefinitionRegistration ToolResultRegistration =>
        AgentHookPointDefinitions.ToolResultRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.OutputValidatingRegistration"/>
    internal static HookPointDefinitionRegistration OutputValidatingRegistration =>
        AgentHookPointDefinitions.OutputValidatingRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeMemoryProposal"/>
    internal static HookPointDefinition<IBeforeMemoryProposalHook, BeforeMemoryProposalEventArgs> BeforeMemoryProposal =>
        AgentHookPointDefinitions.BeforeMemoryProposal;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeMemoryProposalRegistration"/>
    internal static HookPointDefinitionRegistration BeforeMemoryProposalRegistration =>
        AgentHookPointDefinitions.BeforeMemoryProposalRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeMemoryWrite"/>
    internal static HookPointDefinition<IBeforeMemoryWriteHook, BeforeMemoryWriteEventArgs> BeforeMemoryWrite =>
        AgentHookPointDefinitions.BeforeMemoryWrite;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeMemoryWriteRegistration"/>
    internal static HookPointDefinitionRegistration BeforeMemoryWriteRegistration =>
        AgentHookPointDefinitions.BeforeMemoryWriteRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeRetrieval"/>
    internal static HookPointDefinition<IBeforeRetrievalHook, BeforeRetrievalEventArgs> BeforeRetrieval =>
        AgentHookPointDefinitions.BeforeRetrieval;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeRetrievalRegistration"/>
    internal static HookPointDefinitionRegistration BeforeRetrievalRegistration =>
        AgentHookPointDefinitions.BeforeRetrievalRegistration;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeRetrievalExposure"/>
    internal static HookPointDefinition<IBeforeRetrievalExposureHook, BeforeRetrievalExposureEventArgs> BeforeRetrievalExposure =>
        AgentHookPointDefinitions.BeforeRetrievalExposure;

    /// <inheritdoc cref="AgentHookPointDefinitions.BeforeRetrievalExposureRegistration"/>
    internal static HookPointDefinitionRegistration BeforeRetrievalExposureRegistration =>
        AgentHookPointDefinitions.BeforeRetrievalExposureRegistration;
}
