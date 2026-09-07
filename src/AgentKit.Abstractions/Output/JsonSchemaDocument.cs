// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An embedded JSON Schema document used to validate a structured output
/// candidate.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Unlike
/// <see cref="JsonSchemaReference"/>, which points at a schema registered
/// elsewhere by name and version, this type carries the schema body itself,
/// because an <see cref="OutputDefinition"/> is the authoritative source of
/// its own contract rather than a pointer into a separate registry.
/// </para>
/// <para>
/// The first-party <c>AgentKit.Output</c> processor validates candidates
/// against this schema using a practical structural subset of JSON Schema —
/// <c>type</c>, <c>required</c>, <c>properties</c>, and <c>items</c> — not
/// the full draft 2020-12 vocabulary. Keywords such as <c>$ref</c>,
/// <c>allOf</c>/<c>anyOf</c>/<c>oneOf</c>, <c>pattern</c>, and
/// <c>format</c> are not evaluated. This is documented, not silent: schemas
/// relying on unevaluated keywords still parse and pass structural checks,
/// but do not enforce those keywords' constraints.
/// </para>
/// </remarks>
public sealed record JsonSchemaDocument
{
    /// <summary>Initializes a new instance of the <see cref="JsonSchemaDocument"/> record.</summary>
    /// <param name="name">A stable, human-readable name for this schema.</param>
    /// <param name="version">The version of this schema.</param>
    /// <param name="schema">The schema body.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public JsonSchemaDocument(string name, SchemaVersion version, JsonElement schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Version = version;
        Schema = schema;
    }

    /// <summary>Gets a stable, human-readable name for this schema.</summary>
    public string Name { get; init; }

    /// <summary>Gets the version of this schema.</summary>
    public SchemaVersion Version { get; init; }

    /// <summary>Gets the schema body.</summary>
    public JsonElement Schema { get; init; }
}
