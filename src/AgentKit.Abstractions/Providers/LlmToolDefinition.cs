// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Describes one tool advertised to a model in a chat model request,
/// so the model can request it through a <see cref="ToolCallPart"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a deliberately minimal, provider-neutral tool description
/// covering what a chat-completion request must send to advertise a tool.
/// The full tool catalog, resolution, authorization, and invocation
/// pipeline belongs to the not-yet-implemented AgentKit.Tools package; this
/// type exists so a chat model adapter has a stable, self-contained shape
/// to translate without depending on that pipeline.
/// </para>
/// </remarks>
public sealed record LlmToolDefinition
{
    /// <summary>Initializes a new instance of the <see cref="LlmToolDefinition"/> record.</summary>
    /// <param name="id">The stable tool identity.</param>
    /// <param name="name">The tool name to advertise to the model.</param>
    /// <param name="description">A human- and model-readable description of the tool's purpose.</param>
    /// <param name="parametersSchema">The JSON Schema describing the tool's call arguments.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public LlmToolDefinition(
        ToolId id,
        string name,
        string? description,
        JsonElement parametersSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        Description = description;
        ParametersSchema = parametersSchema;
    }

    /// <summary>Gets the stable tool identity.</summary>
    public ToolId Id { get; init; }

    /// <summary>Gets the tool name to advertise to the model.</summary>
    public string Name { get; init; }

    /// <summary>Gets a human- and model-readable description of the tool's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the JSON Schema describing the tool's call arguments.</summary>
    public JsonElement ParametersSchema { get; init; }
}
