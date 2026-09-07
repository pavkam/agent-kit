// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AzureOpenAI.Wire;

/// <summary>
/// The wire shape of an Azure OpenAI HTTP error body: the same
/// OpenAI-shaped <c>{"error":{"code","message","param?","type?"}}</c>
/// envelope used by the public OpenAI API, sometimes with additional
/// Azure-specific details this package does not yet capture.
/// </summary>
internal sealed class AzureOpenAIErrorEnvelopeDto
{
    /// <summary>Gets or sets the nested error detail.</summary>
    [JsonPropertyName("error")]
    public AzureOpenAIErrorDetailDto? Error { get; set; }
}
