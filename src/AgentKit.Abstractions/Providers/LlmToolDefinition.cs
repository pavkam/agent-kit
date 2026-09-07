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
    private JsonElement _parametersSchema;
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
        _parametersSchema = CloneSchema(parametersSchema);
    }

    /// <summary>Gets the stable tool identity.</summary>
    public ToolId Id { get; init; }

    /// <summary>Gets the tool name to advertise to the model.</summary>
    public string Name { get; init; }

    /// <summary>Gets a human- and model-readable description of the tool's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the JSON Schema describing the tool's call arguments.</summary>
    public JsonElement ParametersSchema
    {
        get => _parametersSchema;
        init => _parametersSchema = CloneSchema(value);
    }

    /// <summary>Determines whether this tool has the same advertised identity and schema as <paramref name="other"/>.</summary>
    /// <param name="other">The tool to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every advertised field has equal content.</returns>
    /// <remarks>
    /// Schema equality is deep rather than tied to a <see cref="JsonDocument"/>
    /// instance, so independently parsed catalog content remains comparable.
    /// </remarks>
    public bool Equals(LlmToolDefinition? other) =>
        other is not null
        && Id.Equals(other.Id)
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Description, other.Description, StringComparison.Ordinal)
        && SchemasEqual(ParametersSchema, other.ParametersSchema);

    /// <summary>Returns a hash code consistent with structural tool equality.</summary>
    /// <returns>A hash code over the tool identity and text fields.</returns>
    /// <remarks>
    /// The schema is intentionally omitted. <see cref="JsonElement.DeepEquals(JsonElement, JsonElement)"/>
    /// recognizes equivalent JSON representations whose raw source differs,
    /// so including raw JSON would violate the equality/hash-code contract.
    /// This produces permitted hash collisions for tools that differ only by
    /// schema while retaining stable equality across catalog reconstruction.
    /// </remarks>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Description, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    private static bool SchemasEqual(JsonElement left, JsonElement right) =>
        left.ValueKind == JsonValueKind.Undefined || right.ValueKind == JsonValueKind.Undefined
            ? left.ValueKind == right.ValueKind
            : JsonElement.DeepEquals(left, right);

    private static JsonElement CloneSchema(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Undefined ? default : schema.Clone();
}
