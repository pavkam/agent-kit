// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of a complete, non-streaming OpenAI-compatible chat
/// completion response body (<c>stream: false</c>).
/// </summary>
internal sealed class OpenAIChatCompletionResponse
{
    /// <summary>Gets or sets the provider-supplied response identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the resolved model identifier that actually served the request.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the ordered response choices. Requests pin <c>n</c> to 1,
    /// so the parser requires exactly one choice and fails closed on more.
    /// </summary>
    [JsonPropertyName("choices")]
    public required IReadOnlyList<OpenAIChatCompletionChoice> Choices { get; set; }

    /// <summary>Gets or sets token usage for the completed request, when reported.</summary>
    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }
}
