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
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
