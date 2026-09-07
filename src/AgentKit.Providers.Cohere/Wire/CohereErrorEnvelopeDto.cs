// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of a Cohere HTTP error body: a flat object carrying a
/// single human-readable message.
/// </summary>
internal sealed class CohereErrorEnvelopeDto
{
    /// <summary>Gets or sets the human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
