// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents one immutable revision of an ordered durable work plan.</summary>
public sealed record WorkPlan
{
    /// <summary>Initializes an immutable plan revision.</summary>
    /// <param name="id">The stable plan identity.</param>
    /// <param name="revision">The positive optimistic revision.</param>
    /// <param name="title">The concise plan purpose.</param>
    /// <param name="items">Between one and fifty uniquely identified ordered items.</param>
    /// <param name="author">The authenticated identity that authored this revision.</param>
    /// <param name="updatedAt">The revision timestamp.</param>
    /// <exception cref="ArgumentNullException"><paramref name="author"/> is null.</exception>
    /// <exception cref="ArgumentException">The title or item collection is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The item count is outside one through fifty.</exception>
    public WorkPlan(
        PlanId id,
        PlanRevision revision,
        string title,
        ImmutableArray<WorkPlanItem> items,
        ExecutionIdentity author,
        DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfContainsNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(items.Length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(items.Length, 50);
        ArgumentException.ThrowIfDuplicatePlanItemIds(items);
        ArgumentException.ThrowIfMultipleInProgressPlanItems(items);
        ArgumentNullException.ThrowIfNull(author);
        Id = id;
        Revision = revision;
        Title = title;
        Items = items;
        Author = author;
        UpdatedAt = updatedAt;
    }

    /// <summary>Gets the stable plan identity.</summary>
    public PlanId Id { get; init; }
    /// <summary>Gets the optimistic revision.</summary>
    public PlanRevision Revision { get; init; }
    /// <summary>Gets the concise plan purpose.</summary>
    public string Title { get; init; }
    /// <summary>Gets the ordered work items.</summary>
    public ImmutableArray<WorkPlanItem> Items { get; init; }
    /// <summary>Gets the authenticated revision author.</summary>
    public ExecutionIdentity Author { get; init; }
    /// <summary>Gets the revision timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <inheritdoc/>
    public bool Equals(WorkPlan? other) =>
        other is not null
        && Id == other.Id
        && Revision == other.Revision
        && Title == other.Title
        && Items.SequenceEqual(other.Items)
        && Author.Equals(other.Author)
        && UpdatedAt == other.UpdatedAt;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Revision);
        hash.Add(Title);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        hash.Add(Author);
        hash.Add(UpdatedAt);
        return hash.ToHashCode();
    }
}
