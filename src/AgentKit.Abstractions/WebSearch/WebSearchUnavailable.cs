// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the selected search service was unavailable.</summary>
public sealed record WebSearchUnavailable: WebSearchProviderResult
{
    /// <summary>Initializes an unavailable result.</summary>
    /// <param name="requestId">The settled request.</param>
    /// <param name="safeMessage">A non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public WebSearchUnavailable(WebSearchRequestId requestId, string safeMessage)
        : base(requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
