// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.FileSystem;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Minimal, defensive JSON argument parsing shared by this package's tools.
/// </summary>
/// <remarks>
/// This package has no dependency on a general JSON Schema validator; each
/// method here checks exactly the shape one tool argument needs and
/// reports every failure through an error message rather than throwing, so
/// a tool can always translate malformed input into
/// <see cref="ToolCallOutcomeKind.Failed"/> instead of letting a parsing
/// exception escape.
/// </remarks>
internal static class ToolArguments
{
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

    public static bool TryGetOptionalString(JsonElement arguments, string name, out string? value)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }
}
