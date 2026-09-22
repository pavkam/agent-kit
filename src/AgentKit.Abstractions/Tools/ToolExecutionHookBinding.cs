// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Binds one tool batch to the active hook activation scope and dispatch metadata factory.</summary>
/// <remarks>
/// The tool executor dispatches <see cref="AgentHookPoints.BeforeToolInvocation"/> and
/// <see cref="AgentHookPoints.ToolResult"/> through this binding; the loop supplies it once per batch and does not
/// dispatch those points itself.
/// </remarks>
public sealed record ToolExecutionHookBinding
{
    /// <summary>Initializes hook binding for one tool batch.</summary>
    /// <param name="scope">The nonnull activation scope captured for this run boundary.</param>
    /// <param name="turnCorrelation">The nonnull turn correlation every dispatch must name.</param>
    /// <param name="createDispatchMetadata">Creates dispatch metadata for one hook point and turn correlation.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public ToolExecutionHookBinding(
        HookActivationScope scope,
        InRunOperationCorrelation turnCorrelation,
        Func<HookPointId, InRunOperationCorrelation, HookDispatchMetadata> createDispatchMetadata)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(turnCorrelation);
        ArgumentNullException.ThrowIfNull(createDispatchMetadata);
        Scope = scope;
        TurnCorrelation = turnCorrelation;
        CreateDispatchMetadata = createDispatchMetadata;
    }

    /// <summary>Gets the activation scope shared by every dispatch in this batch.</summary>
    public HookActivationScope Scope { get; }

    /// <summary>Gets the turn correlation every dispatch must use.</summary>
    public InRunOperationCorrelation TurnCorrelation { get; }

    /// <summary>Gets the factory that mints dispatch metadata for one hook point.</summary>
    public Func<HookPointId, InRunOperationCorrelation, HookDispatchMetadata> CreateDispatchMetadata { get; }
}
