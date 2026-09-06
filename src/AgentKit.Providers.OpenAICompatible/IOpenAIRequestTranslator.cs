// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// Translates a provider-neutral <see cref="ChatModelRequest"/> into an
/// OpenAI-compatible chat completions request body.
/// </summary>
/// <remarks>
/// Translation preserves role, part order, and tool-call identity. It never
/// performs I/O, and it never mutates its <c>request</c> or <c>profile</c>
/// arguments; both are read-only inputs to a pure function.
/// </remarks>
public interface IOpenAIRequestTranslator
{
    /// <summary>
    /// Translates <paramref name="request"/> into an OpenAI-compatible
    /// request body.
    /// </summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <param name="profile">The compatibility profile controlling wire-shape choices.</param>
    /// <param name="useStreaming">
    /// Whether the request should ask for a streaming
    /// (<c>stream: true</c>) response.
    /// </param>
    /// <returns>The JSON request body to send as the HTTP request content.</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> contains message content this translator
    /// cannot represent in the OpenAI-compatible wire format.
    /// </exception>
    public JsonObject Translate(ChatModelRequest request, OpenAICompatibilityProfile profile, bool useStreaming);
}
