// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="StructuredGoalResult"/>.</summary>
/// <param name="Summary">The bounded summary.</param>
/// <param name="Data">The typed result fields.</param>
internal sealed record GoalResultDocument(string Summary, ImmutableArray<GoalExtensionDocument> Data)
{
    /// <summary>Converts a result to its persisted form.</summary>
    /// <param name="value">The non-null result.</param>
    /// <returns>The document.</returns>
    internal static GoalResultDocument FromDomain(StructuredGoalResult value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Summary, GoalExtensionDocument.FromDomain(value.Data));
    }

    /// <summary>Restores the result, re-running its validation.</summary>
    /// <returns>The result.</returns>
    internal StructuredGoalResult ToDomain() => new(Summary, GoalExtensionDocument.ToDomain(Data));
}
