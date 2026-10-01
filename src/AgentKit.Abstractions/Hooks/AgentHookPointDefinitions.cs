// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed hook point definitions for the first-party agent loop points named by <see cref="AgentHookPoints"/>.</summary>
public static class AgentHookPointDefinitions
{
    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.RunStarted"/>.</summary>
    public static HookPointDefinition<IRunStartedHook, RunStartedEventArgs> RunStarted { get; } = new(
        AgentHookPoints.RunStarted,
        HookPointKind.Observational,
        HookFailureMode.IsolateAndDiagnose,
        new DefaultAgentHookMutationValidator<RunStartedEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.ContextAssembled"/>.</summary>
    public static HookPointDefinition<IContextAssembledHook, ContextAssembledEventArgs> ContextAssembled { get; } = new(
        AgentHookPoints.ContextAssembled,
        HookPointKind.Observational,
        HookFailureMode.IsolateAndDiagnose,
        new DefaultAgentHookMutationValidator<ContextAssembledEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeModelRequest"/>.</summary>
    public static HookPointDefinition<IBeforeModelRequestHook, BeforeModelRequestEventArgs> BeforeModelRequest { get; } = new(
        AgentHookPoints.BeforeModelRequest,
        HookPointKind.Mutating,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeModelRequestEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeToolInvocation"/>.</summary>
    public static HookPointDefinition<IBeforeToolInvocationHook, BeforeToolInvocationEventArgs> BeforeToolInvocation { get; } = new(
        AgentHookPoints.BeforeToolInvocation,
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeToolInvocationEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.RunStarted"/>.</summary>
    public static HookPointDefinitionRegistration RunStartedRegistration { get; } = new(
        AgentHookPoints.RunStarted,
        typeof(IRunStartedHook),
        typeof(RunStartedEventArgs),
        HookPointKind.Observational,
        HookFailureMode.IsolateAndDiagnose);

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.ContextAssembled"/>.</summary>
    public static HookPointDefinitionRegistration ContextAssembledRegistration { get; } = new(
        AgentHookPoints.ContextAssembled,
        typeof(IContextAssembledHook),
        typeof(ContextAssembledEventArgs),
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

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.ToolResult"/>.</summary>
    public static HookPointDefinition<IToolResultHook, ToolResultHookEventArgs> ToolResult { get; } = new(
        AgentHookPoints.ToolResult,
        HookPointKind.Mutating,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<ToolResultHookEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.ToolResult"/>.</summary>
    public static HookPointDefinitionRegistration ToolResultRegistration { get; } = new(
        AgentHookPoints.ToolResult,
        typeof(IToolResultHook),
        typeof(ToolResultHookEventArgs),
        HookPointKind.Mutating,
        HookFailureMode.FailOperation);

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.OutputValidating"/>.</summary>
    public static HookPointDefinition<IOutputValidatingHook, OutputValidatingEventArgs> OutputValidating { get; } = new(
        AgentHookPoints.OutputValidating,
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<OutputValidatingEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.OutputValidating"/>.</summary>
    public static HookPointDefinitionRegistration OutputValidatingRegistration { get; } = new(
        AgentHookPoints.OutputValidating,
        typeof(IOutputValidatingHook),
        typeof(OutputValidatingEventArgs),
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation);

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeMemoryProposal"/>.</summary>
    public static HookPointDefinition<IBeforeMemoryProposalHook, BeforeMemoryProposalEventArgs> BeforeMemoryProposal { get; } = new(
        AgentHookPoints.BeforeMemoryProposal,
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeMemoryProposalEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.BeforeMemoryProposal"/>.</summary>
    public static HookPointDefinitionRegistration BeforeMemoryProposalRegistration { get; } = new(
        AgentHookPoints.BeforeMemoryProposal,
        typeof(IBeforeMemoryProposalHook),
        typeof(BeforeMemoryProposalEventArgs),
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation);

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeMemoryWrite"/>.</summary>
    public static HookPointDefinition<IBeforeMemoryWriteHook, BeforeMemoryWriteEventArgs> BeforeMemoryWrite { get; } = new(
        AgentHookPoints.BeforeMemoryWrite,
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeMemoryWriteEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.BeforeMemoryWrite"/>.</summary>
    public static HookPointDefinitionRegistration BeforeMemoryWriteRegistration { get; } = new(
        AgentHookPoints.BeforeMemoryWrite,
        typeof(IBeforeMemoryWriteHook),
        typeof(BeforeMemoryWriteEventArgs),
        HookPointKind.ShortCircuiting,
        HookFailureMode.FailOperation);

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeRetrieval"/>.</summary>
    public static HookPointDefinition<IBeforeRetrievalHook, BeforeRetrievalEventArgs> BeforeRetrieval { get; } = new(
        AgentHookPoints.BeforeRetrieval,
        HookPointKind.Mutating,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeRetrievalEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.BeforeRetrieval"/>.</summary>
    public static HookPointDefinitionRegistration BeforeRetrievalRegistration { get; } = new(
        AgentHookPoints.BeforeRetrieval,
        typeof(IBeforeRetrievalHook),
        typeof(BeforeRetrievalEventArgs),
        HookPointKind.Mutating,
        HookFailureMode.FailOperation);

    /// <summary>Gets the closed definition for <see cref="AgentHookPoints.BeforeRetrievalExposure"/>.</summary>
    public static HookPointDefinition<IBeforeRetrievalExposureHook, BeforeRetrievalExposureEventArgs> BeforeRetrievalExposure { get; } = new(
        AgentHookPoints.BeforeRetrievalExposure,
        HookPointKind.Mutating,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<BeforeRetrievalExposureEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    /// <summary>Gets the point-definition registration for <see cref="AgentHookPoints.BeforeRetrievalExposure"/>.</summary>
    public static HookPointDefinitionRegistration BeforeRetrievalExposureRegistration { get; } = new(
        AgentHookPoints.BeforeRetrievalExposure,
        typeof(IBeforeRetrievalExposureHook),
        typeof(BeforeRetrievalExposureEventArgs),
        HookPointKind.Mutating,
        HookFailureMode.FailOperation);
}
