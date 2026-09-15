// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A content part carrying a reference to image, audio, file, or other
/// binary media, as either input supplied to the model or output the model
/// produced.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record MediaReferencePart: ContentPart
{
    /// <summary>Initializes a new instance of the <see cref="MediaReferencePart"/> record.</summary>
    /// <param name="reference">The referenced media item.</param>
    /// <param name="semantics">The role this media plays in the message.</param>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="reference"/> or <paramref name="extensions"/> is null.
    /// </exception>
    public MediaReferencePart(
        MediaReference reference,
        MediaSemantics semantics,
        ExtensionData extensions)
        : base(extensions)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
        Semantics = semantics;
    }

    /// <summary>Gets the referenced media item.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public MediaReference Reference
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the role this media plays in the message.</summary>
    public MediaSemantics Semantics { get; init; }
}
