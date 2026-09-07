// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Wire;

/// <summary>
/// The wire shape of the nested <c>error</c> object in an Azure OpenAI
/// error envelope.
/// </summary>
internal sealed class AzureOpenAIErrorDetailDto
{
    /// <summary>Gets or sets the human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>Gets or sets the provider-supplied error code, when present.</summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>Gets or sets the OpenAI-style error type/category, when present.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
