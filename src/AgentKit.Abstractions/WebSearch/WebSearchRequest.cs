// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries one exact authorized query to a selected web-search provider.</summary>
public sealed record WebSearchRequest
{
    /// <summary>Initializes a bounded provider search request.</summary>
    /// <param name="id">The stable request identity.</param>
    /// <param name="context">The causing tool execution context.</param>
    /// <param name="query">The non-blank query text.</param>
    /// <param name="domains">The ordered canonical domain allow-filter.</param>
    /// <param name="freshness">The portable recency preference.</param>
    /// <param name="maximumResults">The positive result ceiling.</param>
    /// <param name="deadline">The exclusive attempt deadline.</param>
    /// <param name="grant">The exact egress grant the provider must consume.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="query"/> is blank or <paramref name="domains"/> is default.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum is undefined or <paramref name="maximumResults"/> is not positive.</exception>
    public WebSearchRequest(
        WebSearchRequestId id,
        ToolExecutionContext context,
        string query,
        ImmutableArray<NormalizedHost> domains,
        WebSearchFreshness freshness,
        int maximumResults,
        DateTimeOffset deadline,
        SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfDefault(domains);
        ArgumentOutOfRangeException.ThrowIfUndefined(freshness);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        ArgumentNullException.ThrowIfNull(grant);
        Id = id;
        Context = context;
        Query = query;
        Domains = domains;
        Freshness = freshness;
        MaximumResults = maximumResults;
        Deadline = deadline;
        Grant = grant;
    }

    /// <summary>Gets the stable request identity.</summary>
    public WebSearchRequestId Id { get; init; }
    /// <summary>Gets the causing tool execution context.</summary>
    public ToolExecutionContext Context { get; init; }
    /// <summary>Gets the exact query text.</summary>
    public string Query { get; init; }
    /// <summary>Gets the ordered canonical domain allow-filter.</summary>
    public ImmutableArray<NormalizedHost> Domains { get; init; }
    /// <summary>Gets the portable recency preference.</summary>
    public WebSearchFreshness Freshness { get; init; }
    /// <summary>Gets the positive result ceiling.</summary>
    public int MaximumResults { get; init; }
    /// <summary>Gets the exclusive attempt deadline.</summary>
    public DateTimeOffset Deadline { get; init; }
    /// <summary>Gets the exact egress grant the provider must consume.</summary>
    public SecurityGrant Grant { get; init; }

    /// <inheritdoc/>
    public bool Equals(WebSearchRequest? other) =>
        other is not null
        && Id == other.Id
        && Context.Equals(other.Context)
        && Query == other.Query
        && Domains.SequenceEqual(other.Domains)
        && Freshness == other.Freshness
        && MaximumResults == other.MaximumResults
        && Deadline == other.Deadline
        && Grant.Equals(other.Grant);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Context);
        hash.Add(Query);
        foreach (var domain in Domains)
        {
            hash.Add(domain);
        }

        hash.Add(Freshness);
        hash.Add(MaximumResults);
        hash.Add(Deadline);
        hash.Add(Grant);
        return hash.ToHashCode();
    }
}
