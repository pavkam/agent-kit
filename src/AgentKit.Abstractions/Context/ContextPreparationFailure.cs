// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A failure encountered while assembling one provider-ready request, before any provider I/O.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// <see cref="SafeMessage"/> never contains source session content.
/// </remarks>
public sealed record ContextPreparationFailure
{
    /// <summary>Initializes a new instance of the <see cref="ContextPreparationFailure"/> record.</summary>
    /// <param name="kind">The category of this failure.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <param name="extensions">Additional caller-specific or forward-compatible detail.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ContextPreparationFailure(ContextPreparationFailureKind kind, string safeMessage, ExtensionData extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentNullException.ThrowIfNull(extensions);

        Kind = kind;
        SafeMessage = safeMessage;
        Extensions = extensions;
    }

    /// <summary>Gets the category of this failure.</summary>
    public ContextPreparationFailureKind Kind { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }

    /// <summary>Gets additional caller-specific or forward-compatible detail.</summary>
    public ExtensionData Extensions { get; init; }
}
