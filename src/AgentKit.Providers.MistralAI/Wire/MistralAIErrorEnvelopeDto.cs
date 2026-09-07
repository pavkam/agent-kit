// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of a Mistral AI HTTP error body.
/// </summary>
/// <remarks>
/// Mistral's FastAPI-based error contract sends <c>detail</c> as a plain
/// string for an ordinary HTTP exception (authentication, authorization,
/// throttling, and most other failures), but as an array of structured
/// field-validation errors for a <c>422</c> request-validation failure.
/// <see cref="Detail"/> is captured as a raw <see cref="JsonElement"/> so
/// the response parser can distinguish and handle both shapes.
/// </remarks>
internal sealed class MistralAIErrorEnvelopeDto
{
    /// <summary>Gets or sets the raw error detail, either a string or an array of validation errors.</summary>
    [JsonPropertyName("detail")]
    public JsonElement? Detail { get; set; }
}
