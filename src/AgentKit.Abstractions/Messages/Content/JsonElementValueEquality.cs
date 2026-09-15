// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Structural JSON-value equality used by content parts that carry a <see cref="JsonElement"/>,
/// so message equality never depends on the identity of the backing <see cref="JsonDocument"/>.
/// </summary>
/// <remarks>
/// <see cref="JsonElement.DeepEquals"/> rejects <see cref="JsonValueKind.Undefined"/> elements, which
/// content parts legitimately carry when no payload was supplied (the <see langword="default"/>
/// element). This helper treats two undefined elements as equal and an undefined element as
/// unequal to any defined one, then defers to deep structural comparison.
/// </remarks>
internal static class JsonElementValueEquality
{
    /// <summary>Compares two elements by JSON value, tolerating undefined elements.</summary>
    /// <param name="left">The first element.</param>
    /// <param name="right">The second element.</param>
    /// <returns><see langword="true"/> when both are undefined or both are defined and structurally equal.</returns>
    internal static bool Equals(JsonElement left, JsonElement right)
    {
        var leftUndefined = left.ValueKind == JsonValueKind.Undefined;
        var rightUndefined = right.ValueKind == JsonValueKind.Undefined;
        return leftUndefined || rightUndefined
            ? leftUndefined && rightUndefined
            : JsonElement.DeepEquals(left, right);
    }
}
