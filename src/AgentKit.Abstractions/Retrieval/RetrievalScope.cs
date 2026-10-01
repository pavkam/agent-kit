// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Narrows a retrieval to named namespaces and documents inside the visibility the authorization already grants.</summary>
/// <remarks>A scope can only narrow: empty lists mean "everything the caller may see", never "everything that exists". A scope never widens visibility or classification.</remarks>
public sealed record RetrievalScope
{
    /// <summary>Gets the scope that adds no restriction beyond the caller's own visibility.</summary>
    public static RetrievalScope Unrestricted { get; } = new([], []);

    /// <summary>Initializes a validated scope.</summary>
    /// <param name="namespaces">Namespaces to restrict durable memory to, or default for every namespace.</param>
    /// <param name="documents">Documents to restrict document retrieval to, or default for every visible document.</param>
    /// <exception cref="ArgumentException">A namespace is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A document identity is default.</exception>
    public RetrievalScope(ImmutableArray<MemoryNamespace> namespaces, ImmutableArray<DocumentId> documents)
    {
        var declaredNamespaces = namespaces.IsDefault ? [] : namespaces;
        foreach (var scope in declaredNamespaces)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope.Value, nameof(namespaces));
        }

        var declaredDocuments = documents.IsDefault ? [] : documents;
        foreach (var document in declaredDocuments)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(document, default, nameof(documents));
        }

        Namespaces = declaredNamespaces;
        Documents = declaredDocuments;
    }

    /// <summary>Gets the namespaces durable memory is restricted to; empty for every namespace.</summary>
    public ImmutableArray<MemoryNamespace> Namespaces { get; }

    /// <summary>Gets the documents document retrieval is restricted to; empty for every visible document.</summary>
    public ImmutableArray<DocumentId> Documents { get; }

    /// <summary>Determines whether another scope describes the same restrictions, comparing the lists by content.</summary>
    /// <param name="other">The scope to compare.</param>
    /// <returns><see langword="true"/> when both lists match element by element.</returns>
    public bool Equals(RetrievalScope? other) =>
        other is not null && Namespaces.SequenceEqual(other.Namespaces) && Documents.SequenceEqual(other.Documents);

    /// <summary>Returns a hash code consistent with <see cref="Equals(RetrievalScope?)"/>.</summary>
    /// <returns>A hash over both lists.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var scope in Namespaces)
        {
            hash.Add(scope);
        }

        foreach (var document in Documents)
        {
            hash.Add(document);
        }

        return hash.ToHashCode();
    }
}
