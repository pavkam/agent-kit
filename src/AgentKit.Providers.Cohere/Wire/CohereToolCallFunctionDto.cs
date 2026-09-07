// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere.Wire;

/// <summary>
/// The wire shape of the nested <c>function</c> object carried by a
/// <see cref="CohereToolCallDto"/>.
/// </summary>
internal sealed class CohereToolCallFunctionDto
{
    /// <summary>Gets or sets the tool/function name the model chose to call.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the call arguments, a JSON-encoded string (empty on
    /// the first streamed fragment, then accumulated as further fragments
    /// arrive).
    /// </summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}
