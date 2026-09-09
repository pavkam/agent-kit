// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Retains owned structured JSON terminal content and its optional schema reference.</summary>
public sealed record ToolResultStructuredContent: ToolResultContent
{
    /// <summary>Initializes structured terminal content by cloning the supplied JSON value.</summary>
    /// <param name="value">A defined JSON value whose owning document may subsequently be disposed.</param>
    /// <param name="schema">The optional schema reference describing the value.</param>
    /// <param name="extensions">Compatible immutable field evidence.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is undefined.</exception>
    /// <exception cref="ArgumentException">A supplied <paramref name="schema"/> has a blank name or default version.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ToolResultStructuredContent(JsonElement value, JsonSchemaReference? schema, ExtensionData extensions)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(value.ValueKind, JsonValueKind.Undefined, nameof(value));
        if (schema is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(schema.Name, nameof(schema));
            ArgumentException.ThrowIfNullOrWhiteSpace(schema.Version.Value, nameof(schema));
        }
        ArgumentNullException.ThrowIfNull(extensions);
        var owned = value.Clone();
        Schema = schema;
        Extensions = extensions;
        Value = owned;
    }

    /// <summary>Gets the owned structured value.</summary><value>A defined cloned JSON value.</value>
    public JsonElement Value { get; }
    /// <summary>Gets the optional schema reference.</summary><value>Null when no schema was retained.</value>
    public JsonSchemaReference? Schema { get; }
    /// <summary>Gets compatible field evidence.</summary><value>A nonnull immutable bag.</value>
    public ExtensionData Extensions { get; }

    /// <summary>Determines structural JSON and evidence equality.</summary>
    /// <param name="other">The content to compare, or null.</param>
    /// <returns>True when JSON, schema, and extensions are structurally equal.</returns>
    public bool Equals(ToolResultStructuredContent? other) =>
        other is not null && JsonElement.DeepEquals(Value, other.Value) && Schema == other.Schema && Extensions == other.Extensions;

    /// <summary>Returns a hash compatible with structural equality.</summary>
    /// <returns>A hash compatible with structural equality; unequal JSON values may share a hash.</returns>
    public override int GetHashCode()
    {
        Debug.Assert(Value.ValueKind is not JsonValueKind.Undefined, "Construction owns a defined JSON value.");
        return HashCode.Combine(Schema, Extensions);
    }
}
