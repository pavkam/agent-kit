// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The safe, closed evidence a failed facade session-creation result carries.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization. <see cref="Kind"/> is a closed enum, not open text, so a caller can branch on cause
/// without parsing <see cref="SafeMessage"/>; this mirrors <see cref="ContextPreparationFailure"/> and
/// <see cref="OutputSchemaConfigurationFailure"/>'s kind-plus-safe-message shape.
/// </remarks>
public sealed record SessionCreationFailure
{
    /// <summary>Initializes a new instance of the <see cref="SessionCreationFailure"/> record.</summary>
    /// <param name="kind">The category of this failure.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public SessionCreationFailure(SessionCreationFailureKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);

        Kind = kind;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the category of this failure.</summary>
    public SessionCreationFailureKind Kind { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
