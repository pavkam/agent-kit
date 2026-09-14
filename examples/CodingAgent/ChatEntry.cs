// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>The kind of one rendered transcript entry, used to pick its accent color and header style.</summary>
internal enum ChatEntryKind
{
    User,
    Assistant,
    ToolCall,
    ToolResultSuccess,
    ToolResultFailure,
    Error,
    System,
}

/// <summary>One self-contained transcript row: a short header label plus a body.</summary>
/// <param name="Kind">Picks the row's accent color and header style.</param>
/// <param name="HeaderText">The short label shown above <paramref name="Body"/>.</param>
/// <param name="Body">Rendered as Markdown prose when <paramref name="Language"/> is <see langword="null"/> and
/// <paramref name="IsCode"/> is <see langword="false"/>; otherwise rendered verbatim, line-preserving, through
/// <c>CodeView</c> (syntax-colored when <paramref name="Language"/> names a supported grammar, plain monospace
/// text otherwise).</param>
/// <param name="IsCode">Whether <paramref name="Body"/> is source/data text that must keep its exact line
/// structure — Markdown would otherwise join adjacent non-blank lines into one paragraph.</param>
/// <param name="Language">The exact `CodeView` catalog language name for <paramref name="Body"/>, or
/// <see langword="null"/> for unhighlighted plain text (still line-preserving when <paramref name="IsCode"/>).</param>
internal sealed record ChatEntry(ChatEntryKind Kind, string HeaderText, string Body, bool IsCode = false, string? Language = null);
