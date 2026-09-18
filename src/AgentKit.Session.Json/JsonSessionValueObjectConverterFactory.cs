// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Selects <see cref="JsonSessionValueObjectConverter{T}"/> for every AgentKit single-value immutable identity struct.</summary>
/// <remarks>
/// The shape test is deliberately structural rather than a hard-coded type list: every AgentKit domain identity is a
/// nonenum, nonnullable struct exposing exactly one property and exactly one single-argument constructor. Matching the
/// shape keeps newly introduced identities durable without editing this leaf, and excluding enumerations leaves the
/// configured string-enumeration converter in control of their persisted form.
/// </remarks>
internal sealed class JsonSessionValueObjectConverterFactory: JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return typeToConvert.IsValueType
            && !typeToConvert.IsEnum
            && Nullable.GetUnderlyingType(typeToConvert) is null
            && typeToConvert.GetProperties().Length == 1
            && typeToConvert.GetConstructors().Count(static constructor => constructor.GetParameters().Length == 1) == 1;
    }

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        return (JsonConverter) Activator.CreateInstance(
            typeof(JsonSessionValueObjectConverter<>).MakeGenericType(typeToConvert))!;
    }
}
