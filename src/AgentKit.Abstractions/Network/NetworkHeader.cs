// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One request or response header name/value pair.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. A header
/// value must never carry a raw secret; credential material stays inside
/// the owning leaf integration and is applied by mechanisms that keep it
/// out of logs and diagnostics, never through a plainly recorded header
/// value on this type.
/// </remarks>
public sealed record NetworkHeader
{
    /// <summary>Initializes a new instance of the <see cref="NetworkHeader"/> record.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public NetworkHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        Name = name;
        Value = value;
    }

    /// <summary>Gets the header name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the header value.</summary>
    public string Value { get; init; }
}
