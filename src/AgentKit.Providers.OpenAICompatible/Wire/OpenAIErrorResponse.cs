// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible.Wire;

/// <summary>
/// The wire shape of an OpenAI-compatible error response body.
/// </summary>
internal sealed class OpenAIErrorResponse
{
    /// <summary>Gets or sets the nested error detail.</summary>
    [JsonPropertyName("error")]
    public OpenAIErrorDetail? Error { get; set; }
}
