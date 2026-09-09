// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>Owns one JSON Schema document together with its explicit dialect.</summary>
/// <remarks>
/// <para>
/// This value clones the supplied JSON and is safe to retain after its source
/// <see cref="JsonDocument"/> is disposed. JSON objects and the boolean schema
/// roots are representable; other JSON roots are not schemas.
/// </para>
/// <para>
/// Construction checks representation and an optional root
/// <c>$schema</c> declaration only. It performs no schema preflight, reference
/// resolution, vocabulary check, or compilation and is not evidence that any
/// engine supports the document's keywords. Duplicate properties other than
/// <c>$schema</c> remain intact for the selected engine to reject under its
/// own preflight rules.
/// </para>
/// </remarks>
public sealed record JsonSchema
{
    /// <summary>Initializes an owned JSON Schema document.</summary>
    /// <param name="dialect">The explicit nondefault dialect identity.</param>
    /// <param name="document">The object or boolean JSON Schema root to clone.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dialect"/> is default; <paramref name="document"/> is
    /// undefined or has a root other than object or boolean; or a root
    /// <c>$schema</c> declaration is not a string.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A root object repeats <c>$schema</c>, or its declaration does not equal
    /// <paramref name="dialect"/> exactly.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// <paramref name="document"/> depends on a disposed <see cref="JsonDocument"/>.
    /// </exception>
    public JsonSchema(JsonSchemaDialectId dialect, JsonElement document)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(dialect, default);
        ArgumentOutOfRangeException.ThrowIfEqual(document.ValueKind, JsonValueKind.Undefined, nameof(document));
        ArgumentOutOfRangeException.ThrowIfEqual(document.ValueKind, JsonValueKind.Null, nameof(document));
        ArgumentOutOfRangeException.ThrowIfEqual(document.ValueKind, JsonValueKind.String, nameof(document));
        ArgumentOutOfRangeException.ThrowIfEqual(document.ValueKind, JsonValueKind.Number, nameof(document));
        ArgumentOutOfRangeException.ThrowIfEqual(document.ValueKind, JsonValueKind.Array, nameof(document));

        if (document.ValueKind == JsonValueKind.Object)
        {
            ValidateDialectDeclaration(dialect, document);
        }

        var documentClone = document.Clone();
        Dialect = dialect;
        Document = documentClone;
    }

    /// <summary>Gets the explicit dialect identity.</summary>
    /// <value>A nondefault identity that is not inferred from ambient engine configuration.</value>
    public JsonSchemaDialectId Dialect { get; }

    /// <summary>Gets the owned JSON Schema root.</summary>
    /// <value>An object or boolean value cloned during construction.</value>
    public JsonElement Document { get; }

    /// <summary>Compares schemas by explicit dialect and JSON semantic content.</summary>
    /// <param name="other">The schema to compare, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when dialects and document content are equal.</returns>
    public bool Equals(JsonSchema? other) =>
        other is not null && Dialect == other.Dialect && JsonElement.DeepEquals(Document, other.Document);

    /// <summary>Returns a hash code consistent with semantic schema equality.</summary>
    /// <returns>A hash derived from the explicit dialect.</returns>
    /// <remarks>
    /// Equivalent JSON can have different raw spellings and property orders,
    /// so the document is deliberately omitted. Unequal schemas may collide.
    /// </remarks>
    public override int GetHashCode() => Dialect.GetHashCode();

    private static void ValidateDialectDeclaration(JsonSchemaDialectId dialect, JsonElement document)
    {
        Debug.Assert(dialect != default, "A validated explicit dialect is required.");
        Debug.Assert(document.ValueKind == JsonValueKind.Object, "Only an object can declare $schema.");
        var declarations = 0;
        foreach (var property in document.EnumerateObject())
        {
            if (!string.Equals(property.Name, "$schema", StringComparison.Ordinal))
            {
                continue;
            }

            declarations++;
            ArgumentOutOfRangeException.ThrowIfNotEqual(
                property.Value.ValueKind, JsonValueKind.String, nameof(document));
            ArgumentException.ThrowIfNotEqual(property.Value.GetString(), dialect.Value, nameof(document));
        }

        ArgumentException.ThrowIfNotEqual(declarations <= 1, true, nameof(document));
    }
}
