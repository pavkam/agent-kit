// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A monotonic version stamp for one registered process executor profile.</summary>
public readonly record struct ProcessExecutorVersion
{
    /// <summary>Initializes a new instance of the <see cref="ProcessExecutorVersion"/> struct.</summary>
    /// <param name="value">The non-negative version number.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    public ProcessExecutorVersion(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Value = value;
    }

    /// <summary>Gets the version number.</summary>
    public long Value { get; }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
