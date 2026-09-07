// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

/// <summary>
/// Translates a provider-neutral <see cref="LlmModelRequest"/> into a
/// Gemini <c>GenerateContentRequest</c> body.
/// </summary>
/// <remarks>
/// Translation preserves role, content-part order, and tool-call identity.
/// It never performs I/O, and it never mutates its <c>request</c> argument;
/// it is a read-only input to a pure function. Unlike OpenAI-compatible and
/// Anthropic requests, the target model identifier and streaming choice are
/// expressed entirely through the request URL, so the returned body never
/// contains a <c>model</c> or <c>stream</c> field.
/// </remarks>
public interface IGoogleGeminiContentTranslator
{
    /// <summary>
    /// Translates <paramref name="request"/> into a Gemini
    /// <c>GenerateContentRequest</c> body.
    /// </summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <returns>The JSON request body to send as the HTTP request content.</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> contains message content this translator
    /// cannot represent in the Gemini GenerateContent wire format.
    /// </exception>
    public JsonObject Translate(LlmModelRequest request);
}
