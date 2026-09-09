// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Owns one defined JSON value for a configuration setting whose declared schema admits document data.</summary>
/// <remarks>Construction clones data but does not prove the value against a setting schema or grant it selection semantics.</remarks>
public sealed record ConfigurationJsonValue: ConfigurationSemanticValue
{
    /// <summary>Clones a defined JSON scalar, array, or object into independently owned configuration evidence.</summary>
    /// <param name="value">The defined JSON value whose source document may be disposed after construction.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
    public ConfigurationJsonValue(JsonElement value)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value.ValueKind, JsonValueKind.Undefined, nameof(value));
        Value = value.Clone();
    }

    /// <summary>Gets the owned JSON value.</summary>
    /// <value>A defined clone whose lifetime is independent of the caller's document.</value>
    public JsonElement Value { get; }

    /// <summary>Determines structural JSON equality.</summary>
    /// <param name="other">The JSON configuration value to compare, or null.</param>
    /// <returns>True when the retained JSON values are structurally equal.</returns>
    public bool Equals(ConfigurationJsonValue? other) =>
        other is not null && JsonElement.DeepEquals(Value, other.Value);

    /// <summary>Returns a hash compatible with structural JSON equality.</summary>
    /// <returns>A bounded hash; structurally unequal JSON values may share it.</returns>
    public override int GetHashCode()
    {
        Debug.Assert(Value.ValueKind is not JsonValueKind.Undefined, "Construction owns a defined JSON value.");
        return typeof(ConfigurationJsonValue).GetHashCode();
    }
}
