// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An embedded, owned JSON Schema document used to validate a structured output
/// candidate.
/// </summary>
/// <remarks>
/// <para>
/// This immutable value owns a clone of its schema body and compares schema
/// content structurally. It is therefore safe to retain after the
/// <see cref="JsonDocument"/> that supplied the input element is disposed and
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// A selected output validator declares the JSON Schema dialect and capabilities
/// it supports. Schema features outside those capabilities must fail at that
/// validator's validation boundary; this provider-neutral value neither narrows
/// the vocabulary nor silently treats an unsupported keyword as satisfied.
/// </para>
/// </remarks>
public sealed record JsonSchemaDocument
{
    /// <summary>Initializes a new owned schema document.</summary>
    /// <param name="name">The non-empty stable, human-readable schema name.</param>
    /// <param name="version">The initialized durable version of this schema.</param>
    /// <param name="schema">The defined JSON schema body to clone and retain.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="version"/> is its default value or <paramref name="schema"/>
    /// is undefined.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// <paramref name="schema"/> still depends on a disposed <see cref="JsonDocument"/>
    /// when its content is cloned.
    /// </exception>
    public JsonSchemaDocument(string name, SchemaVersion version, JsonElement schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfEqual(version, default);
        ArgumentOutOfRangeException.ThrowIfEqual(schema.ValueKind, JsonValueKind.Undefined, nameof(schema));
        var schemaClone = schema.Clone();

        Name = name;
        Version = version;
        Schema = schemaClone;
    }

    /// <summary>Gets the non-empty stable, human-readable schema name.</summary>
    /// <value>A validated ordinal name retained by this immutable value.</value>
    /// <exception cref="ArgumentNullException">
    /// An object initializer or <see langword="with"/> expression assigns null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An object initializer or <see langword="with"/> expression assigns an empty
    /// or whitespace value.
    /// </exception>
    public string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Name));
            field = value;
        }
    }

    /// <summary>Gets the initialized durable version of this schema.</summary>
    /// <value>A non-default version that identifies this schema's contract.</value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An object initializer or <see langword="with"/> expression assigns the
    /// default <see cref="SchemaVersion"/> value.
    /// </exception>
    public SchemaVersion Version
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value, default, nameof(Version));
            field = value;
        }
    }

    /// <summary>Gets the owned JSON schema body.</summary>
    /// <value>
    /// A defined clone captured from caller input. The returned value remains
    /// readable after the source <see cref="JsonDocument"/> is disposed.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An object initializer or <see langword="with"/> expression assigns an
    /// undefined JSON element.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// An object initializer or <see langword="with"/> expression assigns an
    /// element that still depends on a disposed <see cref="JsonDocument"/>.
    /// </exception>
    public JsonElement Schema
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfEqual(value.ValueKind, JsonValueKind.Undefined, nameof(Schema));
            field = value.Clone();
        }
    }

    /// <summary>Compares this document with <paramref name="other"/> by all semantic fields.</summary>
    /// <param name="other">The document to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when names, versions, and JSON schema content are equal.</returns>
    public bool Equals(JsonSchemaDocument? other) =>
        other is not null
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && Version.Equals(other.Version)
        && JsonElement.DeepEquals(Schema, other.Schema);

    /// <summary>Returns a hash code compatible with structural document equality.</summary>
    /// <returns>A hash code derived from the ordinal name and version.</returns>
    /// <remarks>
    /// The JSON body is intentionally omitted because
    /// <see cref="JsonElement.DeepEquals(JsonElement, JsonElement)"/> recognizes
    /// equivalent JSON representations that can have different raw text. This
    /// permits collisions between unequal schemas while preserving the equality
    /// and hash-code contract.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(
        StringComparer.Ordinal.GetHashCode(Name),
        Version);
}
