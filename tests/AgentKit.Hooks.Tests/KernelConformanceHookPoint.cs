// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks.Tests;

/// <summary>Closed conformance hook point types used only by kernel dispatch tests.</summary>
internal static class KernelConformanceHookPoint
{
    internal static HookPointId Id { get; } = new("agentkit.conformance.kernel.mutating");

    internal static HookPointDefinition<IKernelConformanceHook, KernelConformanceEventArgs> Definition { get; } = new(
        Id,
        HookPointKind.Mutating,
        HookFailureMode.FailOperation,
        new DefaultAgentHookMutationValidator<KernelConformanceEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));

    internal static HookPointDefinitionRegistration Registration { get; } = new(
        Id,
        typeof(IKernelConformanceHook),
        typeof(KernelConformanceEventArgs),
        HookPointKind.Mutating,
        HookFailureMode.FailOperation);

    internal static HookPointDefinition<IKernelObservingHook, KernelObservingEventArgs> ObservingDefinition { get; } = new(
        new HookPointId("agentkit.conformance.kernel.observing"),
        HookPointKind.Observational,
        HookFailureMode.IsolateAndDiagnose,
        new DefaultAgentHookMutationValidator<KernelObservingEventArgs>(),
        static (hook, args, context, cancellationToken) => hook.InvokeAsync(args, context, cancellationToken));
}

internal interface IKernelConformanceHook: IHook
{
    public ValueTask InvokeAsync(KernelConformanceEventArgs args, HookInvocationContext context, CancellationToken cancellationToken);
}

internal interface IKernelObservingHook: IHook
{
    public ValueTask InvokeAsync(KernelObservingEventArgs args, HookInvocationContext context, CancellationToken cancellationToken);
}

internal sealed class KernelConformanceEventArgs: AgentHookEventArgs
{
    public KernelConformanceEventArgs(HookDispatchMetadata dispatch)
        : base(dispatch)
    {
    }

    public List<HookRegistrationId> InvocationOrder { get; } = [];

    public List<HookInvocationId> InvocationIds { get; } = [];
}

internal sealed class KernelObservingEventArgs: AgentHookEventArgs
{
    public KernelObservingEventArgs(HookDispatchMetadata dispatch)
        : base(dispatch)
    {
    }

    public string? Payload { get; set; }

    public override object? CaptureMutableState() => Payload;

    public override void RestoreMutableState(object? snapshot) => Payload = (string?)snapshot;
}
