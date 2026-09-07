// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An extensible request method, such as <see cref="Get"/> or <see cref="Post"/>.</summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// case-insensitive) equality over <see cref="Value"/>. It carries no
/// mutable state itself and is safe to share and compare across threads
/// without synchronization. This is deliberately not a closed enumeration
/// so a transport can support a method beyond the common set declared here
/// without a breaking change.
/// </remarks>
public readonly record struct NetworkMethod
{
    /// <summary>Gets the <c>GET</c> method.</summary>
    public static NetworkMethod Get { get; } = new("GET");

    /// <summary>Gets the <c>POST</c> method.</summary>
    public static NetworkMethod Post { get; } = new("POST");

    /// <summary>Gets the <c>PUT</c> method.</summary>
    public static NetworkMethod Put { get; } = new("PUT");

    /// <summary>Gets the <c>PATCH</c> method.</summary>
    public static NetworkMethod Patch { get; } = new("PATCH");

    /// <summary>Gets the <c>DELETE</c> method.</summary>
    public static NetworkMethod Delete { get; } = new("DELETE");

    /// <summary>Gets the <c>HEAD</c> method.</summary>
    public static NetworkMethod Head { get; } = new("HEAD");

    /// <summary>Gets the <c>OPTIONS</c> method.</summary>
    public static NetworkMethod Options { get; } = new("OPTIONS");

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkMethod"/>
    /// struct, validating and canonicalizing the supplied method text.
    /// </summary>
    /// <param name="value">The non-empty method token text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public NetworkMethod(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim().ToUpperInvariant();
    }

    /// <summary>Gets the canonicalized, uppercase method token text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the method token text, suitable for logging and diagnostic
    /// messages.
    /// </summary>
    public override string ToString() => Value;
}
