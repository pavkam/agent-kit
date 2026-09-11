// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using System.Runtime.InteropServices;
using System.Text.Json;

/// <summary>Owns one nonshared work budget for schema inspection and exact canonical instance evaluation.</summary>
/// <remarks>No state escapes an operation. Every recursive path consumes bounded JSON depth and work; reference and regex keywords are unsupported.</remarks>
internal sealed class ToolSchemaProcessor
{
    private readonly ToolSchemaLimits _limits;
    private readonly CancellationToken _cancellationToken;
    private long _remaining;
    private int _nodes;

    /// <summary>Creates a fresh local processing budget without inspecting content.</summary>
    /// <param name="limits">The nonnull operation limits.</param>
    /// <param name="cancellationToken">The cancellation token checked at every charged unit of work.</param>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is null.</exception>
    internal ToolSchemaProcessor(ToolSchemaLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits; _remaining = limits.MaximumWork; _cancellationToken = cancellationToken;
    }

    /// <summary>Preflights a complete canonical schema with raw input and total-work bounds.</summary>
    /// <param name="schema">The nonnull owned schema whose complete document will be inspected.</param>
    /// <returns>A rejection classification, or null after complete successful inspection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="ToolSchemaResourceLimitException">An input or work ceiling is exceeded.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels during processing.</exception>
    internal ToolSchemaRejectionReason? Preflight(JsonSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return Measure(schema.Document) ? Inspect(schema.Document, true) : ToolSchemaRejectionReason.InvalidSchema;
    }

    private bool Measure(JsonElement value)
    {
        Debug.Assert(value.ValueKind != JsonValueKind.Undefined, "The owning operation validates a live initialized input before measurement.");
        Charge(1);
        return JsonMarshal.GetRawUtf8Value(value).Length > _limits.MaximumUtf8Bytes ? throw new ToolSchemaResourceLimitException() : Walk(value, 1);
    }

    private ToolSchemaRejectionReason? Inspect(JsonElement schema, bool isRoot)
    {
        Debug.Assert(schema.ValueKind != JsonValueKind.Undefined, "Complete bounded measurement precedes schema-position inspection.");
        Charge(1 + JsonMarshal.GetRawUtf8Value(schema).Length);
        if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False) { return null; }
        if (schema.ValueKind != JsonValueKind.Object) { return ToolSchemaRejectionReason.InvalidSchema; }
        foreach (var property in schema.EnumerateObject())
        {
            Charge(1);
            var value = property.Value;
            switch (property.Name)
            {
                case "$schema":
                    if (!isRoot) { return ToolSchemaRejectionReason.UnsupportedKeyword; }
                    if (value.ValueKind != JsonValueKind.String) { return ToolSchemaRejectionReason.InvalidSchema; }
                    if (value.GetString() != BoundedToolSchemaEngine.DialectName) { return ToolSchemaRejectionReason.UnsupportedDialect; }
                    break;
                case "type":
                    if (value.ValueKind == JsonValueKind.String ? !IsType(value.GetString()!)
                        : value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0
                            || !UniqueStrings(value, true)) { return ToolSchemaRejectionReason.InvalidSchema; }
                    break;
                case "properties":
                    if (value.ValueKind != JsonValueKind.Object) { return ToolSchemaRejectionReason.InvalidSchema; }
                    foreach (var child in value.EnumerateObject())
                    {
                        if (Inspect(child.Value, false) is { } error) { return error; }
                    }
                    break;
                case "items":
                case "additionalProperties":
                    if (Inspect(value, false) is { } nestedError) { return nestedError; }
                    break;
                case "required":
                    if (value.ValueKind != JsonValueKind.Array || !UniqueStrings(value, false)) { return ToolSchemaRejectionReason.InvalidSchema; }
                    break;
                case "enum":
                    if (value.ValueKind != JsonValueKind.Array) { return ToolSchemaRejectionReason.InvalidSchema; }
                    // Draft 2020-12 recommends a nonempty unique list but does not require either.
                    break;
                case "const":
                case "default":
                    break;
                case "minimum":
                case "maximum":
                case "exclusiveMinimum":
                case "exclusiveMaximum":
                    if (value.ValueKind != JsonValueKind.Number) { return ToolSchemaRejectionReason.InvalidSchema; }
                    _ = Number(value);
                    break;
                case "minLength":
                case "maxLength":
                case "minItems":
                case "maxItems":
                case "minProperties":
                case "maxProperties":
                    if (value.ValueKind != JsonValueKind.Number) { return ToolSchemaRejectionReason.InvalidSchema; }
                    var number = Number(value);
                    if (!number.IsInteger || !number.IsNonnegative) { return ToolSchemaRejectionReason.InvalidSchema; }
                    break;
                case "uniqueItems":
                case "deprecated":
                case "readOnly":
                case "writeOnly":
                    if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { return ToolSchemaRejectionReason.InvalidSchema; }
                    break;
                case "$comment":
                case "title":
                case "description":
                case "format":
                    if (value.ValueKind != JsonValueKind.String) { return ToolSchemaRejectionReason.InvalidSchema; }
                    break;
                case "examples":
                    if (value.ValueKind != JsonValueKind.Array) { return ToolSchemaRejectionReason.InvalidSchema; }
                    break;
                default:
                    return ToolSchemaRejectionReason.UnsupportedKeyword;
            }
        }
        return null;
    }

    /// <summary>Bounds a complete instance and evaluates the retained canonical schema.</summary>
    /// <param name="schema">The nonnull owned schema retained by the compiled handle.</param>
    /// <param name="instance">An initialized instance with a live owning document.</param>
    /// <returns>False for duplicate members or a violated assertion, otherwise true.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="instance"/> is undefined.</exception>
    /// <exception cref="ToolSchemaResourceLimitException">Input measurement or evaluation exhausts a resource bound.</exception>
    /// <exception cref="OperationCanceledException">The caller cancels during processing.</exception>
    /// <remarks>The compiled handle owns the immutable preflight proof; no current registration or schema metadata is reread.</remarks>
    internal bool Validate(JsonSchema schema, JsonElement instance)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNotEqual(instance.ValueKind != JsonValueKind.Undefined, true, nameof(instance));
        return Measure(instance) && EvaluateCore(schema.Document, instance);
    }

    private bool Walk(JsonElement value, int depth)
    {
        Debug.Assert(value.ValueKind != JsonValueKind.Undefined && depth > 0, "Measurement traverses initialized JSON from a positive root depth.");
        Charge(1 + JsonMarshal.GetRawUtf8Value(value).Length);
        if (depth > Math.Min(_limits.MaximumDepth, 128) || ++_nodes > _limits.MaximumNodes) { throw new ToolSchemaResourceLimitException(); }
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Charge(1 + JsonMarshal.GetRawUtf8PropertyName(property).Length);
                if (!names.Add(property.Name) || !Walk(property.Value, depth + 1)) { return false; }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) { if (!Walk(item, depth + 1)) { return false; } }
        }
        else if (value.ValueKind == JsonValueKind.String) { _ = value.GetString(); }
        return true;
    }

    private bool UniqueStrings(JsonElement array, bool types)
    {
        Debug.Assert(array.ValueKind == JsonValueKind.Array, "Keyword shape validation establishes an array.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            Charge(1 + JsonMarshal.GetRawUtf8Value(item).Length);
            if (item.ValueKind != JsonValueKind.String) { return false; }
            var name = item.GetString()!;
            if (!names.Add(name) || (types && !IsType(name))) { return false; }
        }
        return true;
    }

    private bool EvaluateCore(JsonElement schema, JsonElement instance)
    {
        Debug.Assert(schema.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False, "Only fully preflighted schema nodes reach evaluation.");
        // Charges the bounded linear searches performed for each supported keyword.
        Charge(1 + (32L * JsonMarshal.GetRawUtf8Value(schema).Length) + JsonMarshal.GetRawUtf8Value(instance).Length);
        if (schema.ValueKind == JsonValueKind.True) { return true; }
        if (schema.ValueKind == JsonValueKind.False) { return false; }
        if (schema.TryGetProperty("type", out var type) && !MatchesType(instance, type)) { return false; }
        if (schema.TryGetProperty("const", out var constant) && !Equal(instance, constant)) { return false; }
        if (schema.TryGetProperty("enum", out var options))
        {
            var matched = false;
            foreach (var option in options.EnumerateArray())
            {
                if (Equal(instance, option)) { matched = true; break; }
            }
            if (!matched) { return false; }
        }
        if (instance.ValueKind == JsonValueKind.Number)
        {
            foreach (var keyword in new[] { "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum" })
            {
                if (!schema.TryGetProperty(keyword, out var bound)) { continue; }
                var comparison = Number(instance).CompareTo(Number(bound));
                if ((keyword == "minimum" && comparison < 0) || (keyword == "maximum" && comparison > 0)
                    || (keyword == "exclusiveMinimum" && comparison <= 0) || (keyword == "exclusiveMaximum" && comparison >= 0)) { return false; }
            }
        }
        else if (instance.ValueKind == JsonValueKind.String)
        {
            var text = instance.GetString()!;
            var length = 0;
            foreach (var rune in text.EnumerateRunes()) { Charge(1); length++; }
            if (!CountMatches(schema, "minLength", "maxLength", length)) { return false; }
        }
        else if (instance.ValueKind == JsonValueKind.Array)
        {
            var length = instance.GetArrayLength();
            if (!CountMatches(schema, "minItems", "maxItems", length)) { return false; }
            if (schema.TryGetProperty("uniqueItems", out var unique) && unique.GetBoolean())
            {
                for (var i = 0; i < length; i++)
                {
                    for (var j = i + 1; j < length; j++)
                    {
                        Charge(1 + (2L * JsonMarshal.GetRawUtf8Value(instance).Length));
                        if (Equal(instance[i], instance[j])) { return false; }
                    }
                }
            }
            if (schema.TryGetProperty("items", out var items))
            {
                foreach (var item in instance.EnumerateArray()) { if (!EvaluateCore(items, item)) { return false; } }
            }
        }
        else if (instance.ValueKind == JsonValueKind.Object)
        {
            var count = 0;
            foreach (var property in instance.EnumerateObject()) { Charge(1); count++; }
            if (!CountMatches(schema, "minProperties", "maxProperties", count)) { return false; }
            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var name in required.EnumerateArray())
                {
                    Charge(1 + JsonMarshal.GetRawUtf8Value(instance).Length);
                    if (!instance.TryGetProperty(name.GetString()!, out _)) { return false; }
                }
            }
            var hasProperties = schema.TryGetProperty("properties", out var properties);
            var hasAdditional = schema.TryGetProperty("additionalProperties", out var additional);
            foreach (var property in instance.EnumerateObject())
            {
                Charge(1 + JsonMarshal.GetRawUtf8Value(schema).Length + JsonMarshal.GetRawUtf8PropertyName(property).Length);
                if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
                {
                    if (!EvaluateCore(propertySchema, property.Value)) { return false; }
                }
                else if (hasAdditional && !EvaluateCore(additional, property.Value)) { return false; }
            }
        }
        return true;
    }

    private bool CountMatches(JsonElement schema, string minimum, string maximum, int count)
    {
        Debug.Assert(schema.ValueKind == JsonValueKind.Object && count >= 0, "Keyword counts are established from bounded JSON.");
        if (!schema.TryGetProperty(minimum, out _) && !schema.TryGetProperty(maximum, out _)) { return true; }
        using var document = JsonDocument.Parse(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var actual = Number(document.RootElement);
        return (!schema.TryGetProperty(minimum, out var min) || actual.CompareTo(Number(min)) >= 0)
            && (!schema.TryGetProperty(maximum, out var max) || actual.CompareTo(Number(max)) <= 0);
    }

    private bool MatchesType(JsonElement instance, JsonElement type)
    {
        Debug.Assert(type.ValueKind is JsonValueKind.String or JsonValueKind.Array, "Preflight establishes valid type keywords.");
        if (type.ValueKind == JsonValueKind.String) { return MatchesSingleType(instance, type.GetString()!); }
        foreach (var item in type.EnumerateArray())
        {
            Charge(1);
            if (MatchesSingleType(instance, item.GetString()!)) { return true; }
        }
        return false;
    }

    private bool MatchesSingleType(JsonElement instance, string type)
    {
        Debug.Assert(IsType(type), "Preflight restricts type names.");
        return type switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "null" => instance.ValueKind == JsonValueKind.Null,
            "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "number" => instance.ValueKind == JsonValueKind.Number,
            "integer" => instance.ValueKind == JsonValueKind.Number && Number(instance).IsInteger,
            _ => false,
        };
    }

    private bool Equal(JsonElement left, JsonElement right)
    {
        Debug.Assert(left.ValueKind != JsonValueKind.Undefined && right.ValueKind != JsonValueKind.Undefined, "Equality uses measured instance and schema data.");
        Charge(1 + JsonMarshal.GetRawUtf8Value(left).Length + JsonMarshal.GetRawUtf8Value(right).Length);
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number) { return Number(left).CompareTo(Number(right)) == 0; }
        if (left.ValueKind != right.ValueKind) { return false; }
        if (left.ValueKind == JsonValueKind.String) { return left.GetString() == right.GetString(); }
        if (left.ValueKind == JsonValueKind.Array)
        {
            if (left.GetArrayLength() != right.GetArrayLength()) { return false; }
            var rightItems = right.EnumerateArray();
            foreach (var item in left.EnumerateArray())
            {
                _ = rightItems.MoveNext();
                if (!Equal(item, rightItems.Current)) { return false; }
            }
        }
        else if (left.ValueKind == JsonValueKind.Object)
        {
            var leftCount = 0; var rightCount = 0;
            foreach (var property in right.EnumerateObject()) { Charge(1); rightCount++; }
            foreach (var property in left.EnumerateObject())
            {
                Charge(1 + JsonMarshal.GetRawUtf8Value(right).Length + JsonMarshal.GetRawUtf8PropertyName(property).Length);
                leftCount++;
                if (!right.TryGetProperty(property.Name, out var other) || !Equal(property.Value, other)) { return false; }
            }
            if (leftCount != rightCount) { return false; }
        }
        return true;
    }

    private ToolSchemaNumber Number(JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Number, "Number processing follows schema or candidate kind checks.");
        var length = JsonMarshal.GetRawUtf8Value(value).Length;
        Charge(1L + ((long) length * length));
        return ToolSchemaNumber.Parse(value);
    }

    private void Charge(long work)
    {
        Debug.Assert(work > 0, "Every processing stage charges positive bounded work before allocation or comparison.");
        _cancellationToken.ThrowIfCancellationRequested();
        if (work > _remaining) { throw new ToolSchemaResourceLimitException(); }
        _remaining -= work;
    }

    private static bool IsType(string type)
    {
        Debug.Assert(type is not null, "Type keyword parsing requires a string.");
        return type is "object" or "array" or "string" or "null" or "boolean" or "number" or "integer";
    }
}
