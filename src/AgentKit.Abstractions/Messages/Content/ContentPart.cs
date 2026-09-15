// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one ordered, typed unit of message content: text,
/// media, reasoning, a tool call or result, structured data, or an unknown
/// provider-defined shape.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="TextPart"/>, <see cref="MediaReferencePart"/>,
/// <see cref="ReasoningPart"/>, <see cref="ToolCallPart"/>,
/// <see cref="ToolResultPart"/>, <see cref="StructuredDataPart"/>, and
/// <see cref="UnknownContentPart"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add an eighth kind; a provider-specific shape
/// the core does not recognize is represented through
/// <see cref="UnknownContentPart"/> instead of a new subclass, which is
/// what lets it round-trip through durable storage and compatible provider
/// continuation paths even though the core cannot interpret it.
/// </para>
/// <para>
/// Every content part, and everything it references transitively, is
/// deeply immutable and safe to share across threads without
/// synchronization: parts are constructed once by whatever produced them
/// (a provider adapter parsing a response, a tool invoker recording a
/// result) and never mutated afterward.
/// </para>
/// </remarks>
public abstract record ContentPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPart"/> record.
    /// This constructor is <see langword="private protected"/> so only the
    /// closed set of kinds declared in this assembly can extend the
    /// hierarchy.
    /// </summary>
    /// <param name="extensions">Provider-specific or forward-compatible data.</param>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    private protected ContentPart(ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);
        Extensions = extensions;
    }

    /// <summary>Gets provider-specific or forward-compatible data.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
