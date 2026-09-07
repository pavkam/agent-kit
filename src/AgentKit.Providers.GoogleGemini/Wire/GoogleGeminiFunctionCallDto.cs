// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini.Wire;

/// <summary>
/// The wire shape of the nested <c>functionCall</c> object carried by a
/// <see cref="GoogleGeminiPartDto"/>.
/// </summary>
internal sealed class GoogleGeminiFunctionCallDto
{
    /// <summary>
    /// Gets or sets the provider-supplied call identifier, when the
    /// selected model returns one for parallel-call correlation.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the tool/function name the model chose to call.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>Gets or sets the parsed call argument object.</summary>
    [JsonPropertyName("args")]
    public JsonElement? Args { get; set; }
}
