// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The path and optional query component of one network destination.</summary>
/// <remarks>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </remarks>
public readonly record struct NetworkRoute
{
    /// <summary>Gets the shared instance representing the root path.</summary>
    public static NetworkRoute Root { get; } = new("/");

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkRoute"/> struct,
    /// validating that it is rooted.
    /// </summary>
    /// <param name="value">The path and optional query text, which must begin with <c>/</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> does not begin with <c>/</c>.</exception>
    public NetworkRoute(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.StartsWith('/'))
        {
            throw new ArgumentException("Value must be a rooted path beginning with '/'.", nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the path and optional query text.</summary>
    public string Value { get; }

    /// <summary>
    /// Returns the route text, suitable for logging and diagnostic
    /// messages.
    /// </summary>
    public override string ToString() => Value;
}
