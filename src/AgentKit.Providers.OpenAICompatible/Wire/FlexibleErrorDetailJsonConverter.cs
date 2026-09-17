// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// Reads an OpenAI-compatible error response's <c>error</c> member as an
/// <see cref="OpenAIErrorDetail"/>, whether the wire value is the common
/// nested object shape or a bare JSON string.
/// </summary>
/// <remarks>
/// <para>
/// xAI's chat/embeddings endpoints return errors as
/// <c>{"code":"&lt;status text&gt;","error":"&lt;message&gt;"}</c>: <c>error</c>
/// is a plain string and <c>code</c> is a top-level sibling rather than
/// nested under <c>error</c>. Without this converter,
/// <see cref="System.Text.Json.JsonSerializer"/> throws
/// <see cref="System.Text.Json.JsonException"/> deserializing the whole
/// enclosing <see cref="OpenAIErrorResponse"/>, which previously dropped the
/// provider's code and message entirely and fell back to a status-only
/// failure for every xAI error. A bare string is wrapped into an
/// <see cref="OpenAIErrorDetail"/> carrying only <see cref="OpenAIErrorDetail.Message"/>.
/// </para>
/// <para>
/// This converter is applied via <see cref="JsonConverterAttribute"/> directly on
/// <see cref="OpenAIErrorResponse.Error"/>, not registered globally in
/// <see cref="JsonSerializerOptions.Converters"/>, so deserializing the
/// object shape through the caller-supplied <see cref="JsonSerializerOptions"/>
/// does not re-enter this converter.
/// </para>
/// </remarks>
internal sealed class FlexibleErrorDetailJsonConverter: JsonConverter<OpenAIErrorDetail?>
{
    /// <inheritdoc/>
    public override OpenAIErrorDetail? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => new OpenAIErrorDetail { Message = reader.GetString() },
            JsonTokenType.StartObject => JsonSerializer.Deserialize<OpenAIErrorDetail>(ref reader, options),
            JsonTokenType.None
                or JsonTokenType.EndObject
                or JsonTokenType.StartArray
                or JsonTokenType.EndArray
                or JsonTokenType.PropertyName
                or JsonTokenType.Comment
                or JsonTokenType.Number
                or JsonTokenType.True
                or JsonTokenType.False
                or _ =>
                throw new JsonException($"The error detail must be a JSON object or string, not {reader.TokenType}."),
        };

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, OpenAIErrorDetail? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
