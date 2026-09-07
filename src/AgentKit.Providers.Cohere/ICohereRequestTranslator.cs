// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// Translates a provider-neutral <see cref="LlmModelRequest"/> into a
/// Cohere v2 <c>Chat</c> request body.
/// </summary>
/// <remarks>
/// Translation preserves role, content-part order, and tool-call identity.
/// It never performs I/O, and it never mutates its <c>request</c> argument;
/// it is a read-only input to a pure function. The target model identifier
/// and streaming choice are both expressed as ordinary body fields
/// (<c>model</c> and <c>stream</c>), so <c>useStreaming</c> is a translator
/// input rather than a caller-side URL decision.
/// </remarks>
public interface ICohereRequestTranslator
{
    /// <summary>
    /// Translates <paramref name="request"/> into a Cohere v2
    /// <c>Chat</c> request body.
    /// </summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <param name="useStreaming">Whether the request should set <c>stream: true</c>.</param>
    /// <returns>The JSON request body to send as the HTTP request content.</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> contains message content, a tool-choice
    /// mode, or a setting this translator cannot represent in the Cohere
    /// v2 Chat wire format, including media content, redacted reasoning, a
    /// named forced tool choice (Cohere can only require any tool, not a
    /// specific one), and an explicit parallel-tool-call preference
    /// (Cohere exposes no such control); unsupported content always throws
    /// regardless of the advertised model capabilities.
    /// </exception>
    public JsonObject Translate(LlmModelRequest request, bool useStreaming);
}
