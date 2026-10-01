// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of choosing the configured retrieval sources for a query: the sources, or a content-safe reason none were selected.</summary>
public sealed record RetrievalSourceSelectionResult
{
    private RetrievalSourceSelectionResult(ImmutableArray<IRetrievalSource> sources, string? safeMessage)
    {
        Sources = sources;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the selected sources in the profile's declared order.</summary>
    /// <value>Empty when selection was rejected.</value>
    public ImmutableArray<IRetrievalSource> Sources { get; }

    /// <summary>Gets the content-safe rejection reason, or <see langword="null"/> when sources were selected.</summary>
    public string? SafeMessage { get; }

    /// <summary>Gets a value indicating whether sources were selected.</summary>
    [MemberNotNullWhen(false, nameof(SafeMessage))]
    public bool IsSelected => SafeMessage is null;

    /// <summary>Creates a successful selection.</summary>
    /// <param name="sources">The non-empty selected sources.</param>
    /// <returns>A selected result.</returns>
    /// <exception cref="ArgumentException"><paramref name="sources"/> is default, empty, or contains null.</exception>
    public static RetrievalSourceSelectionResult Selected(ImmutableArray<IRetrievalSource> sources)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(sources);
        ArgumentException.ThrowIfContainsNull(sources);
        return new(sources, null);
    }

    /// <summary>Creates a rejected selection.</summary>
    /// <param name="safeMessage">The content-safe reason.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null or blank.</exception>
    public static RetrievalSourceSelectionResult Rejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return new([], safeMessage);
    }
}
