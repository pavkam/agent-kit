// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json;

/// <summary>Identifies the exact initialized JSON durable-journal root expected by host bootstrap configuration.</summary>
/// <remarks>
/// Verifying the identity in the manifest before every access is what turns an accidentally repointed directory into an
/// immediate failure instead of a journal that answers recovery questions about a different deployment's operations.
/// </remarks>
public readonly record struct JsonDurableStoreInstanceId
{
    /// <summary>Initializes a nondefault store-instance identity.</summary>
    /// <param name="value">The globally unique store identity persisted in the root manifest.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is empty.</exception>
    public JsonDurableStoreInstanceId(Guid value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value, Guid.Empty);
        Value = value;
    }

    /// <summary>Gets the globally unique store identity.</summary>
    /// <value>The nonempty value verified against the manifest during initialization.</value>
    public Guid Value { get; }

    /// <summary>Returns the canonical lowercase identity text.</summary>
    /// <returns>The hyphenated identity used only for safe diagnostics.</returns>
    public override string ToString() => Value.ToString("D");
}
