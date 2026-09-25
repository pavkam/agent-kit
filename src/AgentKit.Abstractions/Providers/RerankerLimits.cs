// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Input limits for one reranker descriptor.</summary>
public sealed record RerankerLimits
{
    /// <summary>Initializes reranker limits.</summary>
    /// <param name="maxDocumentsPerRequest">The maximum documents per request, when bounded.</param>
    /// <param name="maxDocumentCharacters">The maximum characters per document, when bounded.</param>
    public RerankerLimits(int? maxDocumentsPerRequest, int? maxDocumentCharacters)
    {
        if (maxDocumentsPerRequest is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDocumentsPerRequest), maxDocumentsPerRequest, "Value must be at least one.");
        }

        if (maxDocumentCharacters is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDocumentCharacters), maxDocumentCharacters, "Value must be at least one.");
        }

        MaxDocumentsPerRequest = maxDocumentsPerRequest;
        MaxDocumentCharacters = maxDocumentCharacters;
    }

    /// <summary>Gets the maximum documents per request, when bounded.</summary>
    public int? MaxDocumentsPerRequest { get; init; }

    /// <summary>Gets the maximum characters per document, when bounded.</summary>
    public int? MaxDocumentCharacters { get; init; }
}
