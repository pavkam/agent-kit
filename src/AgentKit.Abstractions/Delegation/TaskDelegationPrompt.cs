// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Grant-free, already-authorized child-work envelope delivered to a goal-aware dispatcher.</summary>
public sealed record TaskDelegationPrompt
{
    /// <summary>Initializes one bounded child-work envelope.</summary>
    /// <param name="id">The idempotent delegation identity.</param>
    /// <param name="parentAgentId">The delegating agent.</param>
    /// <param name="parentSessionId">The parent session.</param>
    /// <param name="parentRunId">The active parent run; must equal <paramref name="correlation"/>'s run.</param>
    /// <param name="correlation">The exact in-run causal correlation.</param>
    /// <param name="toolCallId">The model tool call.</param>
    /// <param name="identity">The authenticated execution identity.</param>
    /// <param name="targetAgentId">The explicitly selected child agent.</param>
    /// <param name="objective">The bounded child objective.</param>
    /// <param name="acceptanceCriteria">The non-empty criteria used to validate the result.</param>
    /// <param name="allowedTools">The captured child tool allow-list.</param>
    /// <param name="budget">The requested child ceilings.</param>
    /// <param name="deadline">The absolute child settlement deadline.</param>
    /// <exception cref="ArgumentNullException">A reference value is null.</exception>
    /// <exception cref="ArgumentException">
    /// Text is blank, an array is default, empty, or contains invalid values, or
    /// <paramref name="parentRunId"/> does not equal <paramref name="correlation"/>'s run.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">An identity is empty.</exception>
    public TaskDelegationPrompt(
        DelegationId id,
        AgentId parentAgentId,
        SessionId parentSessionId,
        RunId parentRunId,
        InRunOperationCorrelation correlation,
        ToolCallId toolCallId,
        ExecutionIdentity identity,
        AgentId targetAgentId,
        string objective,
        ImmutableArray<string> acceptanceCriteria,
        ImmutableArray<ToolId> allowedTools,
        TaskDelegationBudget budget,
        DateTimeOffset deadline)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentSessionId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentRunId, default);
        ArgumentNullException.ThrowIfNull(correlation);
        ArgumentException.ThrowIfNotEqual(parentRunId, correlation.RunId, nameof(correlation));
        ArgumentOutOfRangeException.ThrowIfEqual(toolCallId, default);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfEqual(targetAgentId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfDefaultOrEmpty(acceptanceCriteria);
        ArgumentException.ThrowIfContainsNull(acceptanceCriteria);
        ArgumentException.ThrowIfDefault(allowedTools);
        ArgumentNullException.ThrowIfNull(budget);
        Id = id;
        ParentAgentId = parentAgentId;
        ParentSessionId = parentSessionId;
        Correlation = correlation;
        ToolCallId = toolCallId;
        Identity = identity;
        TargetAgentId = targetAgentId;
        Objective = objective;
        AcceptanceCriteria = acceptanceCriteria;
        AllowedTools = allowedTools;
        Budget = budget;
        Deadline = deadline;
    }

    /// <summary>Gets the idempotent delegation identity.</summary>
    public DelegationId Id { get; init; }
    /// <summary>Gets the parent agent.</summary>
    public AgentId ParentAgentId { get; init; }
    /// <summary>Gets the parent session.</summary>
    public SessionId ParentSessionId { get; init; }
    /// <summary>Gets the parent run.</summary>
    /// <value>Read through <see cref="Correlation"/>'s bound run; the constructor requires them to be equal, so this is never a second stored copy.</value>
    public RunId ParentRunId => Correlation.RunId;
    /// <summary>Gets the exact in-run causal correlation.</summary>
    public InRunOperationCorrelation Correlation { get; init; }
    /// <summary>Gets the requesting tool call.</summary>
    public ToolCallId ToolCallId { get; init; }
    /// <summary>Gets the authenticated execution identity.</summary>
    public ExecutionIdentity Identity { get; init; }
    /// <summary>Gets the selected child agent.</summary>
    public AgentId TargetAgentId { get; init; }
    /// <summary>Gets the bounded objective.</summary>
    public string Objective { get; init; }
    /// <summary>Gets the result acceptance criteria.</summary>
    public ImmutableArray<string> AcceptanceCriteria { get; init; }
    /// <summary>Gets the exact child tool allow-list; empty means no tools.</summary>
    public ImmutableArray<ToolId> AllowedTools { get; init; }
    /// <summary>Gets the child execution ceilings.</summary>
    public TaskDelegationBudget Budget { get; init; }
    /// <summary>Gets the absolute settlement deadline.</summary>
    public DateTimeOffset Deadline { get; init; }
}
