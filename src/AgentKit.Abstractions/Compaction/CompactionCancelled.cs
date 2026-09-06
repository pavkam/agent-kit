// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A compaction attempt that was cancelled before it could complete.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record CompactionCancelled: CompactionResult
{
    /// <summary>Initializes a new instance of the <see cref="CompactionCancelled"/> record.</summary>
    /// <param name="context">The operation context this outcome resulted from.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public CompactionCancelled(CompactionOperationContext context, string safeMessage)
        : base(context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
