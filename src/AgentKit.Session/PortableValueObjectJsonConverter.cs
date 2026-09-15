// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Serializes an immutable single-property value object through its validating constructor.</summary>
/// <typeparam name="T">The nonnullable value-object type.</typeparam>
internal sealed class PortableValueObjectJsonConverter<T>: JsonConverter<T>
    where T : struct
{
    private static readonly System.Reflection.PropertyInfo _property = typeof(T).GetProperties().Single();
    private static readonly System.Reflection.ConstructorInfo _constructor = typeof(T).GetConstructors()
        .Single(static candidate => candidate.GetParameters().Length == 1);

    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement.GetProperty(_property.Name);
        var value = element.Deserialize(_property.PropertyType, options)
            ?? throw new JsonException($"The {_property.Name} value for {typeof(T).Name} is null.");
        return (T) _constructor.Invoke([value]);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        writer.WriteStartObject();
        writer.WritePropertyName(_property.Name);
        JsonSerializer.Serialize(writer, _property.GetValue(value), _property.PropertyType, options);
        writer.WriteEndObject();
    }
}
