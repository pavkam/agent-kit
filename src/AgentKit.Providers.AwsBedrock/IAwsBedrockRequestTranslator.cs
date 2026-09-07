// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// Translates a provider-neutral <see cref="LlmModelRequest"/> into a
/// Bedrock Runtime <c>Converse</c>/<c>ConverseStream</c> request body.
/// </summary>
/// <remarks>
/// The Converse and ConverseStream operations share exactly one request
/// body shape; which operation is invoked is selected entirely by the
/// request URL, so this translator does not take a streaming flag the way
/// this repository's Anthropic and OpenAI-compatible translators do.
/// </remarks>
public interface IAwsBedrockRequestTranslator
{
    /// <summary>Translates <paramref name="request"/> into a Converse request body.</summary>
    /// <param name="request">The provider-neutral request to translate.</param>
    /// <returns>The Converse request body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="request"/> uses a message role, content part kind, or
    /// setting this translator does not support translating for the
    /// Bedrock Converse wire format.
    /// </exception>
    public JsonObject Translate(LlmModelRequest request);
}
