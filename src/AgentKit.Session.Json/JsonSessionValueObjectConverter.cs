// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

using System.Globalization;
using System.Reflection;

/// <summary>Persists one immutable single-property AgentKit identity through its own validating constructor.</summary>
/// <typeparam name="T">The nonnullable value-object struct with exactly one property and one single-argument constructor.</typeparam>
/// <remarks>
/// <para>
/// Domain identities such as <see cref="AgentId"/> or <see cref="SessionVersion"/> are structs whose invariants live in a
/// validating constructor. Reflection-based serialization would bypass that constructor and could resurrect a default or
/// out-of-range identity from a hand-edited log, so this converter always routes the decoded payload back through the real
/// constructor.
/// </para>
/// <para>
/// A malformed payload surfaces as <see cref="JsonException"/>. A payload the identity's own constructor rejects surfaces as
/// that constructor's original <see cref="ArgumentException"/>, which the store's observability wrapper maps to corrupt
/// persisted evidence rather than to a caller argument error.
/// </para>
/// </remarks>
internal sealed class JsonSessionValueObjectConverter<T>: JsonConverter<T>
    where T : struct
{
    private static readonly PropertyInfo _property = typeof(T).GetProperties().Single();
    private static readonly ConstructorInfo _constructor = typeof(T).GetConstructors()
        .Single(static candidate => candidate.GetParameters().Length == 1);

    /// <inheritdoc/>
    /// <exception cref="JsonException">The payload is not an object, omits the value property, or carries a null value.</exception>
    /// <exception cref="ArgumentException">The identity's validating constructor rejected the persisted value.</exception>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(PropertyName(options), out var element))
        {
            throw new JsonException($"The persisted {typeof(T).Name} value must be an object carrying its single property.");
        }

        var value = element.Deserialize(_property.PropertyType, options)
            ?? throw new JsonException($"The persisted {typeof(T).Name} value property is null.");
        return (T) _constructor.Invoke(
            BindingFlags.DoNotWrapExceptions, binder: null, [value], CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        writer.WriteStartObject();
        writer.WritePropertyName(PropertyName(options));
        JsonSerializer.Serialize(writer, _property.GetValue(value), _property.PropertyType, options);
        writer.WriteEndObject();
    }

    private static string PropertyName(JsonSerializerOptions options)
    {
        Debug.Assert(options is not null, "The serializer always supplies its effective options.");
        return options.PropertyNamingPolicy?.ConvertName(_property.Name) ?? _property.Name;
    }
}
