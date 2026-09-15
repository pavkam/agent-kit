// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Creates converters for AgentKit single-value immutable struct identities.</summary>
internal sealed class SqliteValueObjectJsonConverterFactory: JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsValueType
        && !typeToConvert.IsEnum
        && Nullable.GetUnderlyingType(typeToConvert) is null
        && typeToConvert.GetProperties().Length == 1
        && typeToConvert.GetConstructors().Count(static constructor => constructor.GetParameters().Length == 1) == 1;

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        ArgumentNullException.ThrowIfNull(options);
        return (JsonConverter) Activator.CreateInstance(
            typeof(SqliteValueObjectJsonConverter<>).MakeGenericType(typeToConvert))!;
    }
}
