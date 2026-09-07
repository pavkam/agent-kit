// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of a Google <c>google.rpc.Status</c>-style error envelope
/// returned as an HTTP error body.
/// </summary>
internal sealed class GoogleGeminiErrorEnvelopeDto
{
    /// <summary>Gets or sets the nested error detail.</summary>
    [JsonPropertyName("error")]
    public GoogleGeminiErrorDetailDto? Error { get; set; }
}
