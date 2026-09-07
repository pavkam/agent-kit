// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Anthropic;

/// <summary>
/// Translates a provider-neutral <see cref="LlmModelRequest"/> into an
/// Anthropic Messages request body.
/// </summary>
/// <remarks>
/// Translation preserves role, content-block order, and tool-call identity.
/// It never performs I/O, and it never mutates its <c>request</c> or
/// <c>options</c> arguments; both are read-only inputs to a pure function.
/// </remarks>
public interface IAnthropicMessageTranslator
{
    /// <summary>
    /// Translates <paramref name="request"/> into an Anthropic Messages
    /// request body.
    /// </summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <param name="options">The Anthropic provider options controlling wire-shape choices.</param>
    /// <param name="useStreaming">
    /// Whether the request should ask for a streaming (<c>stream: true</c>)
    /// response.
    /// </param>
    /// <returns>The JSON request body to send as the HTTP request content.</returns>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> contains message content this translator
    /// cannot represent in the Anthropic Messages wire format.
    /// </exception>
    public JsonObject Translate(LlmModelRequest request, AnthropicProviderOptions options, bool useStreaming);
}
