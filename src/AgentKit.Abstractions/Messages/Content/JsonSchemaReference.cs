// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A named, versioned reference to a registered JSON schema used to
/// validate a <see cref="StructuredDataPart"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. It is
/// deliberately a lightweight pointer rather than an embedded schema
/// document: the actual schema is resolved from a registry by
/// <see cref="Name"/> and <see cref="Version"/>, so many structured-data
/// parts can reference the same schema without duplicating its definition.
/// </remarks>
public sealed record JsonSchemaReference
{
    /// <summary>Initializes a new instance of the <see cref="JsonSchemaReference"/> record.</summary>
    /// <param name="name">The registered schema name.</param>
    /// <param name="version">The registered schema version.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public JsonSchemaReference(string name, SchemaVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Version = version;
    }

    /// <summary>Gets the registered schema name.</summary>
    public string Name { get; init; }

    /// <summary>Gets the registered schema version.</summary>
    public SchemaVersion Version { get; init; }
}
