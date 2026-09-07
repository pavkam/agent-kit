// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one complete or explicitly bounded successful provider response.</summary>
public sealed record WebSearchSucceeded: WebSearchProviderResult
{
    /// <summary>Initializes a successful ordered result set.</summary>
    /// <param name="requestId">The settled request.</param>
    /// <param name="items">The ordered result candidates.</param>
    /// <param name="complete">Whether the provider states the set was complete within the request.</param>
    /// <exception cref="ArgumentException"><paramref name="items"/> is default or contains null.</exception>
    public WebSearchSucceeded(WebSearchRequestId requestId, ImmutableArray<WebSearchItem> items, bool complete)
        : base(requestId)
    {
        ArgumentException.ThrowIfContainsNull(items);
        Items = items;
        Complete = complete;
    }

    /// <summary>Gets the ordered result candidates.</summary>
    public ImmutableArray<WebSearchItem> Items { get; init; }
    /// <summary>Gets whether the provider states the set was complete within the request.</summary>
    public bool Complete { get; init; }

    /// <inheritdoc/>
    public bool Equals(WebSearchSucceeded? other) =>
        other is not null
        && RequestId == other.RequestId
        && Items.SequenceEqual(other.Items)
        && Complete == other.Complete;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RequestId);
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        hash.Add(Complete);
        return hash.ToHashCode();
    }
}
