// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI.Wire;

/// <summary>
/// The wire shape of the nested <c>function</c> object carried by a
/// <see cref="MistralAIToolCallDto"/>.
/// </summary>
internal sealed class MistralAIToolCallFunctionDto
{
    /// <summary>Gets or sets the tool/function name the model chose to call.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the call arguments, documented as either a JSON-encoded
    /// string (the shape Mistral's own examples consistently return) or a
    /// parsed JSON object.
    /// </summary>
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}
