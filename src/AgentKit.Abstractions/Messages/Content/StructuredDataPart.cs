// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A content part carrying structured JSON data, such as a structured
/// output result the model produced against a configured schema.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization. Structured output validation and repair happen
/// upstream of this record; by the time a <see cref="StructuredDataPart"/>
/// exists, its <see cref="Value"/> is expected to already be the validated
/// (or explicitly accepted best-effort) result.
/// </remarks>
public sealed record StructuredDataPart: ContentPart
{
    /// <summary>Initializes a new instance of the <see cref="StructuredDataPart"/> record.</summary>
    /// <param name="value">The structured JSON value.</param>
    /// <param name="schema">
    /// The schema the value was validated against, when one was
    /// configured; <see langword="null"/> when the value is unconstrained
    /// structured data.
    /// </param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public StructuredDataPart(
        JsonElement value,
        JsonSchemaReference? schema,
        ExtensionData extensions)
        : base(extensions)
    {
        Value = value;
        Schema = schema;
    }

    /// <summary>Gets the structured JSON value.</summary>
    public JsonElement Value { get; init; }

    /// <summary>
    /// Gets the schema the value was validated against, when one was
    /// configured.
    /// </summary>
    public JsonSchemaReference? Schema { get; init; }
}
