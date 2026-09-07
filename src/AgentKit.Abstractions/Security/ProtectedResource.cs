// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one canonical resource to which authority is narrowly bound.</summary>
public sealed record ProtectedResource
{
    /// <summary>Initializes a protected resource.</summary>
    /// <param name="kind">The resource class.</param>
    /// <param name="identifier">The non-empty canonical identifier produced by the effecting boundary.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is blank.</exception>
    public ProtectedResource(ProtectedResourceKind kind, string identifier)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        Kind = kind;
        Identifier = identifier;
    }

    /// <summary>Gets the resource class.</summary>
    public ProtectedResourceKind Kind { get; init; }

    /// <summary>Gets the canonical identifier.</summary>
    public string Identifier { get; init; }
}
