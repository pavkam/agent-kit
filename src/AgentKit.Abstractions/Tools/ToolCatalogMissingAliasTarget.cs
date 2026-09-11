// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an authored alias with no descriptor in its toolset's selected source publications.</summary>
/// <remarks>A merge policy cannot repair this case by introducing a new descriptor or borrowing an unrelated toolset's membership.</remarks>
public sealed record ToolCatalogMissingAliasTarget: ToolCatalogCollision
{
    /// <summary>Retains the exact authored mapping that could not be bound.</summary>
    /// <param name="toolset">The nonnull originating toolset publication.</param>
    /// <param name="assignment">The nonnull exact assignment present in that publication.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="assignment"/> is not present in the toolset.</exception>
    public ToolCatalogMissingAliasTarget(ToolsetPublication toolset, ToolAliasAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(toolset);
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentException.ThrowIfNotEqual(toolset.Aliases.Contains(assignment), true, nameof(assignment));
        Toolset = toolset;
        Assignment = assignment;
    }

    /// <summary>Gets the publication that authored the unresolved mapping.</summary>
    /// <value>The immutable source-membership and execution-policy evidence.</value>
    public ToolsetPublication Toolset { get; }

    /// <summary>Gets the exact unresolved alias and requested identity.</summary>
    /// <value>The original assignment, without an invented replacement target.</value>
    public ToolAliasAssignment Assignment { get; }
}
