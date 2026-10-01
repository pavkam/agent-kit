// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the bounded textual body of one durable memory together with provider-neutral extension data.</summary>
/// <remarks>Only text is stored as durable memory content; unsupported media is never stringified into it. The text is untrusted data wherever it is later exposed and never gains instruction authority.</remarks>
public sealed record MemoryContent
{
    /// <summary>The largest number of UTF-16 code units accepted in <see cref="Text"/>.</summary>
    public const int MaximumTextLength = 32_768;

    /// <summary>Initializes validated memory content.</summary>
    /// <param name="text">The non-blank body, at most <see cref="MaximumTextLength"/> code units.</param>
    /// <param name="extensions">Additional extension fields, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="text"/> exceeds <see cref="MaximumTextLength"/>.</exception>
    public MemoryContent(string text, ExtensionData? extensions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, MaximumTextLength, nameof(text));
        Text = text;
        Extensions = extensions ?? ExtensionData.Empty;
    }

    /// <summary>Gets the placeholder a store leaves behind after physically purging a memory's body.</summary>
    /// <value>Content whose text states that the body was purged. It never contains the purged text.</value>
    public static MemoryContent Purged { get; } = new("[purged]");

    /// <summary>Gets the memory body.</summary>
    public string Text { get; }

    /// <summary>Gets the provider-neutral extension data.</summary>
    public ExtensionData Extensions { get; }
}
