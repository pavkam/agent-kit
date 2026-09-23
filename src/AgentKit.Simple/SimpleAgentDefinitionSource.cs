// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>Publishes the agent definitions described by a <see cref="SimpleAgentPlan"/>.</summary>
/// <remarks>Resolved once from DI after the plan is complete; the snapshot is computed at construction and never changes.</remarks>
internal sealed class SimpleAgentDefinitionSource: IAgentDefinitionSource
{
    /// <summary>Initializes the source from the finished plan.</summary>
    /// <param name="plan">The builder-time plan.</param>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The plan is incomplete: no model was selected or no identity is available.</exception>
    public SimpleAgentDefinitionSource(SimpleAgentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        plan.Validate();
        Snapshot = new AgentDefinitionSourceSnapshot(
            SourceId,
            new AgentDefinitionSourceVersion(0),
            0,
            [plan.Definition(), .. plan.AdditionalAgents.Select(agent => plan.DefinitionFor(agent.Key, agent.Value))]);
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
