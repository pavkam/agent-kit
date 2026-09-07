// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a provider or protocol failure after authorization.</summary>
public sealed record WebSearchFailed: WebSearchProviderResult
{
    /// <summary>Initializes a failed result.</summary>
    /// <param name="requestId">The settled request.</param>
    /// <param name="safeMessage">A non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public WebSearchFailed(WebSearchRequestId requestId, string safeMessage)
        : base(requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
