// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>
/// Validates a JSON instance against a practical structural subset of JSON
/// Schema: <c>type</c>, <c>required</c>, <c>properties</c>, and
/// <c>items</c>.
/// </summary>
/// <remarks>
/// See <see cref="JsonSchemaDocument"/> for the reduced-scope rationale:
/// this is not a full draft 2020-12 validator. Keywords such as
/// <c>$ref</c>, <c>allOf</c>/<c>anyOf</c>/<c>oneOf</c>, <c>pattern</c>, and
/// <c>format</c> are not evaluated.
/// </remarks>
internal static class StructuralJsonSchemaValidator
{
    /// <summary>Validates <paramref name="instance"/> against <paramref name="schema"/>.</summary>
    /// <param name="instance">The JSON instance to validate.</param>
    /// <param name="schema">The schema body.</param>
    /// <returns>Every structural issue found, in traversal order.</returns>
    public static ImmutableArray<OutputValidationIssue> Validate(JsonElement instance, JsonElement schema)
    {
        var issues = ImmutableArray.CreateBuilder<OutputValidationIssue>();
        ValidateNode(instance, schema, "$", issues);
        return issues.ToImmutable();
    }

    private static void ValidateNode(
        JsonElement instance, JsonElement schema, string path, ImmutableArray<OutputValidationIssue>.Builder issues)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (schema.TryGetProperty("type", out var typeElement) && !MatchesType(instance, typeElement))
        {
            issues.Add(new OutputValidationIssue(
                "type-mismatch", $"Value at '{path}' does not match the declared type.", path));
            return;
        }

        if (instance.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(instance, schema, path, issues);
        }
        else if (instance.ValueKind == JsonValueKind.Array
            && schema.TryGetProperty("items", out var itemsSchema)
            && itemsSchema.ValueKind == JsonValueKind.Object)
        {
            ValidateArrayItems(instance, itemsSchema, path, issues);
        }
    }

    private static void ValidateObject(
        JsonElement instance, JsonElement schema, string path, ImmutableArray<OutputValidationIssue>.Builder issues)
    {
        if (schema.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProperty in requiredElement.EnumerateArray())
            {
                var name = requiredProperty.GetString();
                if (name is not null && !instance.TryGetProperty(name, out _))
                {
                    issues.Add(new OutputValidationIssue(
                        "required-property-missing", $"Required property '{name}' is missing at '{path}'.", path));
                }
            }
        }

        if (schema.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                if (instance.TryGetProperty(property.Name, out var propertyValue))
                {
                    ValidateNode(propertyValue, property.Value, $"{path}.{property.Name}", issues);
                }
            }
        }
    }

    private static void ValidateArrayItems(
        JsonElement instance, JsonElement itemsSchema, string path, ImmutableArray<OutputValidationIssue>.Builder issues)
    {
        var index = 0;
        foreach (var item in instance.EnumerateArray())
        {
            ValidateNode(item, itemsSchema, $"{path}[{index}]", issues);
            index++;
        }
    }

    private static bool MatchesType(JsonElement instance, JsonElement typeElement)
    {
        if (typeElement.ValueKind == JsonValueKind.String)
        {
            return MatchesSingleType(instance, typeElement.GetString());
        }

        if (typeElement.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (var candidate in typeElement.EnumerateArray())
        {
            if (MatchesSingleType(instance, candidate.GetString()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSingleType(JsonElement instance, string? typeName) => typeName switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && IsInteger(instance),
        _ => true,
    };

    private static bool IsInteger(JsonElement element) =>
        element.TryGetInt64(out _) || (element.TryGetDouble(out var doubleValue) && doubleValue == Math.Floor(doubleValue));
}
