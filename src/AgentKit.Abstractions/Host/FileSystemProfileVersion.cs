// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A monotonic version stamp for one immutable file-system profile snapshot.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over
/// <see cref="Value"/>.
/// </remarks>
public readonly record struct FileSystemProfileVersion
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemProfileVersion"/>
    /// struct, validating that it represents a usable version number.
    /// </summary>
    /// <param name="value">The non-negative profile version.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public FileSystemProfileVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the profile version number.</summary>
    public long Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
