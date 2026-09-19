// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Selects every required per-agent collaborator by its stable, keyed dependency-injection registration.</summary>
/// <remarks>
/// <para>
/// An agent definition selects behavior already registered in dependency injection through typed component keys;
/// it never embeds a service instance directly. This is the closed, final shape every one of these selections
/// belongs in — earlier interim flat properties on <see cref="AgentDefinition"/> (<c>LoopKey</c>,
/// <c>InputCoordinatorKey</c>, <c>OutputPublisherKey</c>) exist only until a later chunk migrates every
/// construction site onto this record.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over every field. It carries no mutable state
/// and is safe to share and compare across threads without synchronization.
/// </para>
/// </remarks>
public sealed record AgentComponentSelection
{
    /// <summary>Captures every required collaborator selection, validating that none is a default, unusable key.</summary>
    /// <param name="loop">The keyed <see cref="IAgentLoop"/> this agent runs with.</param>
    /// <param name="continuationPolicy">The keyed <see cref="IRunContinuationPolicy"/> that decides each turn's continuation.</param>
    /// <param name="input">The keyed <see cref="IInputCoordinator"/> that admits and promotes this agent's input.</param>
    /// <param name="output">The keyed <see cref="IOutputPublisher"/> that publishes this agent's run events and final result.</param>
    /// <param name="outputProcessor">The keyed <see cref="IOutputProcessor"/> that validates this agent's structured output.</param>
    /// <param name="context">The keyed <see cref="IContextAssembler"/> that assembles this agent's model request context.</param>
    /// <param name="modelSelector">The keyed <see cref="IModelSelector"/> that resolves this agent's model for each request.</param>
    /// <param name="modelExecutor">The keyed <see cref="IModelRequestExecutor"/> that executes this agent's model requests.</param>
    /// <param name="budgetProfile">The named budget-limit hierarchy and accounting policy this agent runs under.</param>
    /// <exception cref="ArgumentOutOfRangeException">A required selection is a default, unusable key.</exception>
    public AgentComponentSelection(
        ComponentKey<IAgentLoop> loop,
        ComponentKey<IRunContinuationPolicy> continuationPolicy,
        ComponentKey<IInputCoordinator> input,
        ComponentKey<IOutputPublisher> output,
        ComponentKey<IOutputProcessor> outputProcessor,
        ComponentKey<IContextAssembler> context,
        ComponentKey<IModelSelector> modelSelector,
        ComponentKey<IModelRequestExecutor> modelExecutor,
        BudgetProfileKey budgetProfile)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(loop, default);
        ArgumentOutOfRangeException.ThrowIfEqual(continuationPolicy, default);
        ArgumentOutOfRangeException.ThrowIfEqual(input, default);
        ArgumentOutOfRangeException.ThrowIfEqual(output, default);
        ArgumentOutOfRangeException.ThrowIfEqual(outputProcessor, default);
        ArgumentOutOfRangeException.ThrowIfEqual(context, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelSelector, default);
        ArgumentOutOfRangeException.ThrowIfEqual(modelExecutor, default);
        ArgumentOutOfRangeException.ThrowIfEqual(budgetProfile, default);
        Loop = loop;
        ContinuationPolicy = continuationPolicy;
        Input = input;
        Output = output;
        OutputProcessor = outputProcessor;
        Context = context;
        ModelSelector = modelSelector;
        ModelExecutor = modelExecutor;
        BudgetProfile = budgetProfile;
    }

    /// <summary>Gets the keyed loop this agent runs with.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IAgentLoop> Loop { get; }

    /// <summary>Gets the keyed continuation policy that decides each turn's continuation.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IRunContinuationPolicy> ContinuationPolicy { get; }

    /// <summary>Gets the keyed input coordinator that admits and promotes this agent's input.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IInputCoordinator> Input { get; }

    /// <summary>Gets the keyed output publisher that publishes this agent's run events and final result.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IOutputPublisher> Output { get; }

    /// <summary>Gets the keyed output processor that validates this agent's structured output.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IOutputProcessor> OutputProcessor { get; }

    /// <summary>Gets the keyed context assembler that assembles this agent's model request context.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IContextAssembler> Context { get; }

    /// <summary>Gets the keyed model selector that resolves this agent's model for each request.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IModelSelector> ModelSelector { get; }

    /// <summary>Gets the keyed model request executor that executes this agent's model requests.</summary>
    /// <value>A nondefault component key.</value>
    public ComponentKey<IModelRequestExecutor> ModelExecutor { get; }

    /// <summary>Gets the named budget-limit hierarchy and accounting policy this agent runs under.</summary>
    /// <value>A nondefault profile key.</value>
    public BudgetProfileKey BudgetProfile { get; }
}
