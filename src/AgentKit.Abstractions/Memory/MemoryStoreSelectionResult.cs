// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics.CodeAnalysis;

/// <summary>Is the outcome of selecting one memory store: the store, or a content-safe reason none was selected.</summary>
public sealed record MemoryStoreSelectionResult
{
    private MemoryStoreSelectionResult(IMemoryStore? store, string? safeMessage)
    {
        Store = store;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the selected store, or <see langword="null"/> when selection was rejected.</summary>
    public IMemoryStore? Store { get; }

    /// <summary>Gets the content-safe rejection reason, or <see langword="null"/> when a store was selected.</summary>
    public string? SafeMessage { get; }

    /// <summary>Gets a value indicating whether a store was selected.</summary>
    [MemberNotNullWhen(true, nameof(Store))]
    [MemberNotNullWhen(false, nameof(SafeMessage))]
    public bool IsSelected => Store is not null;

    /// <summary>Creates a successful selection.</summary>
    /// <param name="store">The selected store.</param>
    /// <returns>A selected result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public static MemoryStoreSelectionResult Selected(IMemoryStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return new(store, null);
    }

    /// <summary>Creates a rejected selection.</summary>
    /// <param name="safeMessage">The content-safe reason.</param>
    /// <returns>A rejected result.</returns>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null or blank.</exception>
    public static MemoryStoreSelectionResult Rejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return new(null, safeMessage);
    }
}
