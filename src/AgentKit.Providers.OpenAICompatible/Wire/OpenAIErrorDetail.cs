// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of the nested <c>error</c> object in an OpenAI-compatible
/// error response body.
/// </summary>
internal sealed class OpenAIErrorDetail
{
    /// <summary>Gets or sets the human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Gets or sets the provider's error type/category string.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the request parameter the error relates to, when applicable.</summary>
    [JsonPropertyName("param")]
    public string? Param { get; set; }

    /// <summary>Gets or sets the provider's stable machine-readable error code, when supplied.</summary>
    /// <remarks>
    /// Some OpenAI-compatible servers (notably OpenRouter) send this as a JSON number carrying the
    /// HTTP status rather than a short string; <see cref="FlexibleErrorCodeJsonConverter"/> accepts
    /// either wire form.
    /// </remarks>
    [JsonPropertyName("code")]
    [JsonConverter(typeof(FlexibleErrorCodeJsonConverter))]
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets provider-specific error metadata not covered by the common OpenAI shape.
    /// </summary>
    /// <remarks>
    /// OpenRouter carries its own error category here as <c>metadata.error_type</c> instead of the
    /// common <see cref="Type"/> member, which it does not send.
    /// </remarks>
    [JsonPropertyName("metadata")]
    public OpenAIErrorMetadata? Metadata { get; set; }
}

/// <summary>Provider-specific error metadata nested under an OpenAI-compatible error object.</summary>
internal sealed class OpenAIErrorMetadata
{
    /// <summary>Gets or sets OpenRouter's error category, used as a kind-mapping fallback when <see cref="OpenAIErrorDetail.Type"/> is absent.</summary>
    [JsonPropertyName("error_type")]
    public string? ErrorType { get; set; }
}
