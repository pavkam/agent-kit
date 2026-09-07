// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of one <c>data:</c> line's JSON payload in a
/// server-sent-events <c>POST /v1/chat/completions</c> response with
/// <c>stream: true</c>.
/// </summary>
internal sealed class MistralAICompletionChunkDto
{
    /// <summary>Gets or sets the provider-supplied completion identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the model that actually served the request.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>Gets or sets token usage, when reported at this point (normally only on the final chunk).</summary>
    [JsonPropertyName("usage")]
    public MistralAIUsageDto? Usage { get; set; }

    /// <summary>Gets or sets the ordered response choices. Only the first is used.</summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<MistralAIStreamChoiceDto>? Choices { get; set; }
}
