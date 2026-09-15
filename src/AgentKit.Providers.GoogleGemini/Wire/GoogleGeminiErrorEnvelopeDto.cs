// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of a Google <c>google.rpc.Status</c>-style error envelope
/// returned as an HTTP error body: <c>{ "error": { code, message, status } }</c>.
/// </summary>
/// <remarks>
/// This envelope is shared by every Google API surface AgentKit integrates
/// with, including the Gemini Developer API and Google Cloud Vertex AI, so
/// it is public and reused rather than re-declared per provider package.
/// It is a mutable deserialization target only; it is never retained past
/// the failure-construction call that reads it.
/// </remarks>
public sealed class GoogleGeminiErrorEnvelopeDto
{
    /// <summary>
    /// Gets or sets the nested error detail, or <see langword="null"/> when
    /// the body did not carry an <c>error</c> object.
    /// </summary>
    [JsonPropertyName("error")]
    public GoogleGeminiErrorDetailDto? Error { get; set; }
}
