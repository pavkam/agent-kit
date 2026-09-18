// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Identifies the exact initialized JSON session-directory root expected by host bootstrap configuration.</summary>
/// <remarks>
/// Routing records decide which store owns a session, so a swapped directory root would silently repoint live sessions. The
/// identity is written into the directory manifest once and compared on every initialization, which turns that mistake into
/// a fail-closed bootstrap error.
/// </remarks>
public readonly record struct JsonSessionDirectoryInstanceId
{
    /// <summary>Initializes a nondefault directory-instance identity.</summary>
    /// <param name="value">The globally unique directory identity persisted in the manifest.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public JsonSessionDirectoryInstanceId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique directory identity.</summary>
    /// <value>The nonempty value verified against the manifest on every directory initialization.</value>
    public Guid Value { get; }

    /// <summary>Returns the canonical lowercase identity text.</summary>
    /// <returns>The hyphenated identity used only for safe diagnostics.</returns>
    public override string ToString() => Value.ToString("D");
}
