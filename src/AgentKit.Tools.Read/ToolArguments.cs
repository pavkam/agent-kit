// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

using System.Diagnostics.CodeAnalysis;

/// <summary>Defensively parses the JSON arguments accepted by <see cref="ReadFileTool"/>.</summary>
internal static class ToolArguments
{
    /// <summary>Reads a required, nonempty string property from a JSON argument object.</summary>
    /// <param name="arguments">The candidate JSON argument value.</param>
    /// <param name="name">The property name to read.</param>
    /// <param name="value">The parsed nonempty string when this method succeeds; otherwise, <see langword="null"/>.</param>
    /// <param name="error">A model-safe validation message when this method fails; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the named property contains a nonempty string; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetRequiredString(
        JsonElement arguments, string name, [NotNullWhen(true)] out string? value, [NotNullWhen(false)] out string? error)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            value = null;
            error = "Arguments must be a JSON object.";
            return false;
        }

        if (!arguments.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            value = null;
            error = $"A string property '{name}' is required.";
            return false;
        }

        value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = null;
            error = $"Property '{name}' must not be empty.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Reads an optional 32-bit integer property from a JSON argument object.</summary>
    /// <param name="arguments">The candidate JSON argument value.</param>
    /// <param name="name">The property name to read.</param>
    /// <param name="value">The parsed integer, or <see langword="null"/> when the property is absent or explicitly null.</param>
    /// <param name="error">A model-safe validation message when a present value is not an integer; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the property is absent, null, or a 32-bit integer; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetOptionalInt(
        JsonElement arguments, string name, out int? value, [NotNullWhen(false)] out string? error)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty(name, out var property))
        {
            value = null;
            error = null;
            return true;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            value = null;
            error = null;
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var parsed))
        {
            value = null;
            error = $"Property '{name}' must be an integer.";
            return false;
        }

        value = parsed;
        error = null;
        return true;
    }
}
