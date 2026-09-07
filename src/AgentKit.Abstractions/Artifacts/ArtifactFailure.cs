// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides a stable artifact failure classification and non-sensitive explanation.</summary>
public sealed record ArtifactFailure
{
    /// <summary>Initializes a safe failure.</summary>
    /// <param name="kind">The stable classification.</param>
    /// <param name="safeMessage">The non-sensitive explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public ArtifactFailure(ArtifactFailureKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Kind = kind;
        SafeMessage = safeMessage;
    }
    /// <summary>Gets the stable classification.</summary>
    public ArtifactFailureKind Kind { get; }
    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; }
}
