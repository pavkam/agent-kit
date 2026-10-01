// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The event raised before a retrieval query is authorized and searched.</summary>
/// <remarks>
/// The query, scope, classification ceiling, and destination are read-only. A hook may only narrow the effective budget
/// through <see cref="MaximumItems"/> and <see cref="MaximumBytes"/>; raising either above its starting value is an invalid
/// mutation that fails the retrieval, and a hook cannot widen scope or classification.
/// </remarks>
public sealed class BeforeRetrievalEventArgs: AgentHookEventArgs, IAgentScopedHookStage
{
    /// <summary>Initializes the event arguments.</summary>
    /// <param name="dispatch">The dispatch identity for <see cref="AgentHookPoints.BeforeRetrieval"/>.</param>
    /// <param name="query">The query about to be authorized.</param>
    /// <param name="effectiveBudget">The budget after the profile ceiling was applied; the writable limits start here.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public BeforeRetrievalEventArgs(HookDispatchMetadata dispatch, RetrievalQuery query, RetrievalBudget effectiveBudget)
        : base(dispatch)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(effectiveBudget);
        Query = query;
        EffectiveBudget = effectiveBudget;
        MaximumItems = effectiveBudget.MaximumItems;
        MaximumBytes = effectiveBudget.MaximumBytes;
    }

    /// <summary>Gets the agent that issued the query.</summary>
    public AgentId AgentId => Query.Context.AgentId;

    /// <summary>Gets the session the query came from, when it has one.</summary>
    public SessionId? SessionId => Query.Context.SessionId;

    /// <summary>Gets the query about to be authorized.</summary>
    public RetrievalQuery Query { get; }

    /// <summary>Gets the budget the writable limits started from.</summary>
    public RetrievalBudget EffectiveBudget { get; }

    /// <summary>Gets or sets the maximum number of candidates the retrieval may return; it may only decrease.</summary>
    public int MaximumItems { get; set; }

    /// <summary>Gets or sets the maximum candidate content bytes the retrieval may return; it may only decrease.</summary>
    public int MaximumBytes { get; set; }

    /// <summary>Gets whether a hook narrowed either limit.</summary>
    public bool BudgetNarrowed => MaximumItems != EffectiveBudget.MaximumItems || MaximumBytes != EffectiveBudget.MaximumBytes;

    /// <inheritdoc/>
    public override void Validate()
    {
        if (MaximumItems < 1 || MaximumItems > EffectiveBudget.MaximumItems)
        {
            throw new HookValidationException(
                $"A before-retrieval hook may only narrow the item limit within 1 and {EffectiveBudget.MaximumItems}; it produced {MaximumItems}.");
        }

        if (MaximumBytes < 1 || MaximumBytes > EffectiveBudget.MaximumBytes)
        {
            throw new HookValidationException(
                $"A before-retrieval hook may only narrow the byte limit within 1 and {EffectiveBudget.MaximumBytes}; it produced {MaximumBytes}.");
        }
    }

    /// <inheritdoc/>
    public override object? CaptureMutableState() => (MaximumItems, MaximumBytes);

    /// <inheritdoc/>
    public override void RestoreMutableState(object? snapshot)
    {
        if (snapshot is ValueTuple<int, int> captured)
        {
            MaximumItems = captured.Item1;
            MaximumBytes = captured.Item2;
        }
    }
}
