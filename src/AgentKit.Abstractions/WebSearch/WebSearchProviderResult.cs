// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the closed terminal result vocabulary of one provider search attempt.</summary>
public abstract record WebSearchProviderResult
{
    private protected WebSearchProviderResult(WebSearchRequestId requestId) => RequestId = requestId;

    /// <summary>Gets the request identity this result settles.</summary>
    public WebSearchRequestId RequestId { get; init; }
}
