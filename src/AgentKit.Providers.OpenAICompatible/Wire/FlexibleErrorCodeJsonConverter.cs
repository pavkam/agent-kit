// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

using System.Globalization;

/// <summary>
/// Reads an OpenAI-compatible error object's <c>code</c> member as its textual form, whether the
/// wire value is a JSON string or a JSON number.
/// </summary>
/// <remarks>
/// Most OpenAI-compatible servers send <c>code</c> as a short string
/// (<c>"invalid_api_key"</c>). OpenRouter's documented shape instead sends the numeric HTTP status
/// as <c>code</c> (for example <c>429</c>), both in ordinary error response bodies and in
/// mid-stream error chunks sent with an HTTP 200 status. Without this converter,
/// <see cref="System.Text.Json.JsonSerializer"/> throws <see cref="System.Text.Json.JsonException"/>
/// deserializing the whole enclosing object whenever a compatible server takes the numeric form,
/// which previously misreported every OpenRouter error as a malformed response instead of surfacing
/// the provider's actual code and message.
/// </remarks>
internal sealed class FlexibleErrorCodeJsonConverter: JsonConverter<string?>
{
    /// <inheritdoc/>
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var integer)
                ? integer.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.None
                or JsonTokenType.StartObject
                or JsonTokenType.EndObject
                or JsonTokenType.StartArray
                or JsonTokenType.EndArray
                or JsonTokenType.PropertyName
                or JsonTokenType.Comment
                or JsonTokenType.True
                or JsonTokenType.False
                or _ =>
                throw new JsonException($"The error code must be a JSON string or number, not {reader.TokenType}."),
        };

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}
