// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a vector index for the nearest vectors to a query vector inside the authorized tenant, agent, and principal visibility.</summary>
/// <remarks>The request names the complete vector space the query was embedded in. An index rejects a descriptor that does not match its own before it searches, so a query embedded by a different model revision is never compared with stored vectors.</remarks>
public sealed record VectorSearchRequest
{
    /// <summary>The largest number of matches one request may ask for.</summary>
    public const int MaximumTopK = 200;

    /// <summary>Initializes a validated search request.</summary>
    /// <param name="space">The complete vector space of the query.</param>
    /// <param name="query">The query vector, with the space's dimensions and finite components.</param>
    /// <param name="topK">The number of matches to return, between one and <see cref="MaximumTopK"/>.</param>
    /// <param name="documents">Documents to restrict to, or default for every visible document.</param>
    /// <param name="grant">The single-use grant for this exact read.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The query is default, has the wrong dimension or a non-finite component, a document identity is default, or the grant lacks captured authorization.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is out of range.</exception>
    public VectorSearchRequest(VectorSpaceDescriptor space, ImmutableArray<float> query, int topK, ImmutableArray<DocumentId> documents, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentException.ThrowIfDefaultOrEmpty(query, nameof(query));
        if (query.Length != space.Dimensions)
        {
            throw new ArgumentException("The query must have the space's dimensions.", nameof(query));
        }

        foreach (var component in query)
        {
            if (!float.IsFinite(component))
            {
                throw new ArgumentException("Query components must be finite.", nameof(query));
            }
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(topK, MaximumTopK);
        var declaredDocuments = documents.IsDefault ? [] : documents;
        foreach (var document in declaredDocuments)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(document, default, nameof(documents));
        }

        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Space = space;
        Query = query;
        TopK = topK;
        Documents = declaredDocuments;
        Grant = grant;
    }

    /// <summary>Gets the complete vector space of the query.</summary>
    public VectorSpaceDescriptor Space { get; }

    /// <summary>Gets the query vector.</summary>
    public ImmutableArray<float> Query { get; }

    /// <summary>Gets the number of matches to return.</summary>
    public int TopK { get; }

    /// <summary>Gets the documents to restrict to; empty for every visible document.</summary>
    public ImmutableArray<DocumentId> Documents { get; }

    /// <summary>Gets the single-use grant for this exact read.</summary>
    public SecurityGrant Grant { get; }
}
