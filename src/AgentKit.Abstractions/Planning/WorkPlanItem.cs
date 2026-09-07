// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one ordered, stable, status-bearing unit of planned work.</summary>
public sealed record WorkPlanItem
{
    /// <summary>Initializes one work-plan item.</summary>
    /// <param name="id">The stable item identity.</param>
    /// <param name="text">The concrete work description.</param>
    /// <param name="status">The current progress state.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    public WorkPlanItem(PlanItemId id, string text, PlanItemStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        Id = id;
        Text = text;
        Status = status;
    }

    /// <summary>Gets the stable item identity.</summary>
    public PlanItemId Id { get; init; }
    /// <summary>Gets the concrete work description.</summary>
    public string Text { get; init; }
    /// <summary>Gets the current progress state.</summary>
    public PlanItemStatus Status { get; init; }
}
