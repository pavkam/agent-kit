// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Publishes the one agent definition described by a <see cref="SimpleAgentPlan"/>, advertising every registered tool.</summary>
/// <remarks>Resolved once from DI after the plan is complete; the snapshot is computed at construction and never changes.</remarks>
internal sealed class SimpleAgentDefinitionSource: IAgentDefinitionSource
{
    /// <summary>Initializes the source from the finished plan and the registered tools.</summary>
    /// <param name="plan">The builder-time plan.</param>
    /// <param name="tools">Every tool registered on the service collection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> or <paramref name="tools"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The plan is incomplete: no model was selected or no identity is available.</exception>
    public SimpleAgentDefinitionSource(SimpleAgentPlan plan, IEnumerable<ITool> tools)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(tools);
        plan.Validate();
        var definitions = tools.Select(static tool => tool.Descriptor).ToImmutableArray().ToLlmToolDefinitions();
        Snapshot = new AgentDefinitionSourceSnapshot(
            SourceId,
            new AgentDefinitionSourceVersion(0),
            0,
            [plan.Definition(definitions), .. plan.AdditionalAgents.Select(agent => plan.DefinitionFor(agent.Key, agent.Value, definitions))]);
    }

    /// <inheritdoc/>
    public AgentDefinitionSourceId SourceId { get; } = new("agentkit.simple");

    /// <summary>Gets the immutable bootstrap snapshot the catalog materializes without I/O.</summary>
    public AgentDefinitionSourceSnapshot Snapshot { get; }

    /// <inheritdoc/>
    public ValueTask<AgentDefinitionSourceSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Snapshot);
    }
}
