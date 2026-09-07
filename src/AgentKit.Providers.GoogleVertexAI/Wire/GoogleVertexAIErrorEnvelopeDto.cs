// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI.Wire;

/// <summary>
/// The wire shape of a Vertex AI <c>google.rpc.Status</c>-style error
/// envelope returned as an HTTP error body.
/// </summary>
internal sealed class GoogleVertexAIErrorEnvelopeDto
{
    /// <summary>Gets or sets the nested error detail.</summary>
    [JsonPropertyName("error")]
    public GoogleVertexAIErrorDetailDto? Error { get; set; }
}
