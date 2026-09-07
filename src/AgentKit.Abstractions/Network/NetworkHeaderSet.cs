// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An ordered, immutable collection of request or response headers.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Header
/// name lookup is case-insensitive per RFC 9110, while the original,
/// as-supplied order and casing of every header is preserved for transport
/// and diagnostics.
/// </remarks>
public sealed record NetworkHeaderSet
{
    /// <summary>Gets the shared instance carrying no headers.</summary>
    public static NetworkHeaderSet Empty { get; } = new([]);

    /// <summary>Initializes a new instance of the <see cref="NetworkHeaderSet"/> record.</summary>
    /// <param name="headers">The ordered headers this set carries.</param>
    /// <exception cref="ArgumentException"><paramref name="headers"/> is a default, uninitialized array.</exception>
    public NetworkHeaderSet(ImmutableArray<NetworkHeader> headers)
    {
        ArgumentException.ThrowIfDefault(headers);
        Headers = headers;
    }

    /// <summary>Gets the ordered headers this set carries.</summary>
    public ImmutableArray<NetworkHeader> Headers { get; init; }

    /// <summary>Gets every value for a header name, using case-insensitive comparison.</summary>
    /// <param name="name">The header name to look up.</param>
    /// <returns>Every matching value, in the order they appear in <see cref="Headers"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or consists only of whitespace.</exception>
    public ImmutableArray<string> GetValues(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return [.. Headers.Where(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            .Select(static header => header.Value)];
    }

    /// <inheritdoc/>
    public bool Equals(NetworkHeaderSet? other) => other is not null && Headers.SequenceEqual(other.Headers);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var header in Headers)
        {
            hash.Add(header);
        }

        return hash.ToHashCode();
    }
}
