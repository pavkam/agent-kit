// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Text;

/// <summary>Carries the textual body of one retrieval candidate with its measured size.</summary>
/// <remarks>Candidate content is untrusted data. It is text only: the pipeline never stringifies unsupported media into it, and it never gains instruction authority.</remarks>
public sealed record CandidateContent
{
    /// <summary>Initializes validated candidate content.</summary>
    /// <param name="text">The non-blank text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    public CandidateContent(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
        Utf8Bytes = Encoding.UTF8.GetByteCount(text);
    }

    /// <summary>Gets the candidate text.</summary>
    public string Text { get; }

    /// <summary>Gets the UTF-8 size of <see cref="Text"/> in bytes.</summary>
    public int Utf8Bytes { get; }
}
