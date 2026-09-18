// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Identifies the exact initialized JSON session-store root expected by host bootstrap configuration.</summary>
/// <remarks>
/// The identity is written into the store manifest once at initialization and compared before every access, so a root that
/// was replaced, restored from an unrelated backup, or pointed at a different deployment is rejected instead of silently
/// serving another deployment's session history.
/// </remarks>
public readonly record struct JsonSessionStoreInstanceId
{
    /// <summary>Initializes a nondefault store-instance identity.</summary>
    /// <param name="value">The globally unique store identity persisted in the manifest.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public JsonSessionStoreInstanceId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique store identity.</summary>
    /// <value>The nonempty value verified against the manifest on every store initialization.</value>
    public Guid Value { get; }

    /// <summary>Returns the canonical lowercase identity text.</summary>
    /// <returns>The hyphenated identity used only for safe diagnostics.</returns>
    public override string ToString() => Value.ToString("D");
}
