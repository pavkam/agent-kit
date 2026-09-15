// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of one <c>data:</c> JSON payload in a streaming
/// OpenAI-compatible chat completion response (<c>stream: true</c>).
/// </summary>
internal sealed class OpenAIChatCompletionChunk
{
    /// <summary>Gets or sets the provider-supplied response identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the resolved model identifier that actually served the request.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the ordered response choice fragments carried by this
    /// chunk. Requests pin <c>n</c> to 1, so the parser accepts only a single
    /// fragment for choice index 0 and fails closed on any other candidate.
    /// This is empty on the final usage-only chunk some deployments send when
    /// usage reporting is requested.
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<OpenAIChatCompletionChunkChoice>? Choices { get; set; }

    /// <summary>
    /// Gets or sets token usage for the request, reported only on the final
    /// chunk when usage-in-stream was requested.
    /// </summary>
    [JsonPropertyName("usage")]
    public OpenAIUsage? Usage { get; set; }

    /// <summary>
    /// Gets or sets an error object delivered as a stream frame. Some deployments (OpenRouter, several
    /// gateways) report mid-stream failures this way instead of closing the connection with an HTTP error.
    /// </summary>
    [JsonPropertyName("error")]
    public OpenAIErrorDetail? Error { get; set; }
}
