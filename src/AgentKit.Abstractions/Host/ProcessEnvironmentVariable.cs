// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Contains one explicitly projected non-secret environment value.</summary>
public sealed record ProcessEnvironmentVariable
{
    /// <summary>Initializes one projected environment value.</summary>
    /// <param name="name">The non-blank platform variable name.</param>
    /// <param name="value">The non-null value supplied only to the child process.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank or either value contains NUL.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ProcessEnvironmentVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfContainsNul(name);
        ArgumentException.ThrowIfContainsNul(value);
        Name = name;
        Value = value;
    }

    /// <summary>Gets the projected variable name.</summary>
    public string Name { get; }

    /// <summary>Gets the private child-process value; security evidence contains only its fingerprint.</summary>
    public string Value { get; }
}
