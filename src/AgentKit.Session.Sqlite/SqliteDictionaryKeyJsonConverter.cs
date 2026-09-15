// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Encodes one dictionary key as base64url JSON without relying on display formatting.</summary>
/// <typeparam name="T">The nonnull immutable key type.</typeparam>
internal sealed class SqliteDictionaryKeyJsonConverter<T>: JsonConverter<T>
    where T : notnull
{
    /// <inheritdoc/>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return Decode(document.RootElement);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, KeyOptions());

    /// <inheritdoc/>
    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var encoded = reader.GetString();
        ArgumentException.ThrowIfNullOrWhiteSpace(encoded);
        using var document = JsonDocument.Parse(Convert.FromBase64String(encoded));
        return Decode(document.RootElement);
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value, KeyOptions())));
    }

    /// <summary>Creates isolated field-aware settings for one standalone immutable key.</summary>
    /// <returns>Serializer settings that cannot recurse through this converter.</returns>
    private static JsonSerializerOptions KeyOptions() => new() { IncludeFields = true };

    /// <summary>Constructs a domain key from its canonical JSON properties.</summary>
    /// <param name="element">The parsed key object.</param>
    /// <returns>The reconstructed nondefault key.</returns>
    private static T Decode(JsonElement element)
    {
        if (typeof(T) == typeof(SessionAddress))
        {
            var agent = element.GetProperty("AgentId").GetProperty("Value").GetGuid();
            var session = element.GetProperty("SessionId").GetProperty("Value").GetGuid();
            return (T) (object) new SessionAddress(new AgentId(agent), new SessionId(session));
        }

        if (typeof(T) == typeof((TenantId, AgentId, IdempotencyKey)))
        {
            var tenant = new TenantId(element.GetProperty("Item1").GetProperty("Value").GetString()!);
            var agent = new AgentId(element.GetProperty("Item2").GetProperty("Value").GetGuid());
            var key = new IdempotencyKey(element.GetProperty("Item3").GetProperty("Value").GetString()!);
            return (T) (object) (tenant, agent, key);
        }

        if (typeof(T) == typeof((TenantId, SessionAddress, IdempotencyKey)))
        {
            var tenant = new TenantId(element.GetProperty("Item1").GetProperty("Value").GetString()!);
            var addressElement = element.GetProperty("Item2");
            var address = new SessionAddress(
                new AgentId(addressElement.GetProperty("AgentId").GetProperty("Value").GetGuid()),
                new SessionId(addressElement.GetProperty("SessionId").GetProperty("Value").GetGuid()));
            var key = new IdempotencyKey(element.GetProperty("Item3").GetProperty("Value").GetString()!);
            return (T) (object) (tenant, address, key);
        }

        var value = element.GetProperty("Value");
        var constructor = typeof(T).GetConstructors().Single(static candidate => candidate.GetParameters().Length == 1);
        var parameterType = constructor.GetParameters()[0].ParameterType;
        object argument = parameterType == typeof(Guid)
            ? value.GetGuid()
            : parameterType == typeof(long)
                ? value.GetInt64()
                : parameterType == typeof(int)
                    ? value.GetInt32()
                    : value.GetString()!;
        return (T) constructor.Invoke([argument]);
    }
}
