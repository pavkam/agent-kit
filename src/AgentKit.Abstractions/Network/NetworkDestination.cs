// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The canonical scheme, host, port, and route of one network destination.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A
/// resolver or transport treats every field as a security input: a grant
/// or policy decision bound to one destination never silently authorizes a
/// different scheme, host, port, or route, including one reached only
/// through a redirect.
/// </remarks>
public sealed record NetworkDestination
{
    /// <summary>Initializes a new instance of the <see cref="NetworkDestination"/> record.</summary>
    /// <param name="scheme">The URI scheme, such as <c>"https"</c>.</param>
    /// <param name="host">The canonicalized destination host.</param>
    /// <param name="port">The destination port.</param>
    /// <param name="route">The path and optional query component.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="scheme"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is not between 1 and 65535 inclusive.
    /// </exception>
    public NetworkDestination(string scheme, NormalizedHost host, int port, NetworkRoute route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        Scheme = scheme.Trim().ToLowerInvariant();
        Host = host;
        Port = port;
        Route = route;
    }

    /// <summary>Gets the canonicalized, lowercase URI scheme.</summary>
    public string Scheme { get; init; }

    /// <summary>Gets the canonicalized destination host.</summary>
    public NormalizedHost Host { get; init; }

    /// <summary>Gets the destination port.</summary>
    public int Port { get; init; }

    /// <summary>Gets the path and optional query component.</summary>
    public NetworkRoute Route { get; init; }

    /// <summary>
    /// Returns the canonical absolute URI text for this destination,
    /// suitable for logging and diagnostic messages.
    /// </summary>
    public override string ToString() => $"{Scheme}://{Host}:{Port}{Route}";
}
