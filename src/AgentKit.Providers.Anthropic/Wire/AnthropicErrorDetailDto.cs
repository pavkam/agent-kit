// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic.Wire;

/// <summary>
/// The wire shape of the nested <c>error</c> object in an Anthropic error
/// envelope, whether returned as an HTTP error body or as an in-stream
/// <c>error</c> event.
/// </summary>
internal sealed class AnthropicErrorDetailDto
{
    /// <summary>
    /// Gets or sets Anthropic's stable error type, such as
    /// <c>"invalid_request_error"</c> or <c>"rate_limit_error"</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
