// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Expresses a portable recency preference for web-search results.</summary>
public enum WebSearchFreshness
{
    /// <summary>No recency filter is requested.</summary>
    Any,
    /// <summary>Prefer or require results from roughly the preceding day.</summary>
    Day,
    /// <summary>Prefer or require results from roughly the preceding week.</summary>
    Week,
    /// <summary>Prefer or require results from roughly the preceding month.</summary>
    Month,
    /// <summary>Prefer or require results from roughly the preceding year.</summary>
    Year,
}
