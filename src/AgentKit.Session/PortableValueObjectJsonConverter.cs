// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Serializes an immutable single-property value object through its validating constructor.</summary>
/// <typeparam name="T">The nonnullable value-object type.</typeparam>
/// <remarks>
/// A malformed payload (a non-object, a missing or null property) surfaces as a
/// <see cref="JsonException"/>, and a payload the value object's own constructor rejects surfaces
/// as that constructor's original <see cref="ArgumentException"/> rather than a reflection
/// <see cref="TargetInvocationException"/>, so a codec can map both to typed rejections.
/// </remarks>
internal sealed class PortableValueObjectJsonConverter<T>: JsonConverter<T>
    where T : struct
{
    private static readonly PropertyInfo _property = typeof(T).GetProperties().Single();
    private static readonly ConstructorInfo _constructor = typeof(T).GetConstructors()
        .Single(static candidate => candidate.GetParameters().Length == 1);

    /// <inheritdoc/>
    /// <exception cref="JsonException">
    /// The payload is not an object, lacks the value property, or carries a null value.
    /// </exception>
    /// <exception cref="ArgumentException">The value object's constructor rejected the decoded value.</exception>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(_property.Name, out var element))
        {
            throw new JsonException($"The {typeof(T).Name} payload must be an object with a {_property.Name} property.");
        }

        var value = element.Deserialize(_property.PropertyType, options)
            ?? throw new JsonException($"The {_property.Name} value for {typeof(T).Name} is null.");
        return (T) _constructor.Invoke(BindingFlags.DoNotWrapExceptions, binder: null, [value], CultureInfo.InvariantCulture);
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
