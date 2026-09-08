// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One named schema alternative within an <see cref="OutputMode.Union"/> output definition.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. See
/// <see cref="OutputMode.Union"/> for the reduced-scope rationale: the
/// first-party processor declares this shape but does not yet select among
/// alternatives.
/// </remarks>
public sealed record OutputAlternative
{
    /// <summary>Initializes a new instance of the <see cref="OutputAlternative"/> record.</summary>
    /// <param name="name">The name this alternative is selected by.</param>
    /// <param name="schema">The schema this alternative's candidate must validate against.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or consists only of whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> or <paramref name="schema"/> is null.
    /// </exception>
    public OutputAlternative(string name, JsonSchemaDocument schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(schema);

        Name = name;
        Schema = schema;
    }

    /// <summary>Gets the name this alternative is selected by.</summary>
    /// <value>Non-empty text that distinguishes the alternative within its union.</value>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set empty or whitespace-only text.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
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

    /// <summary>Gets the schema this alternative's candidate must validate against.</summary>
    /// <value>The non-null owned schema retained by this alternative.</value>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public JsonSchemaDocument Schema
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Schema));
            field = value;
        }
    }
}
