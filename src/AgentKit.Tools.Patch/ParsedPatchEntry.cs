// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Contains one side-effect-free parsed file mutation.</summary>
/// <param name="Kind">The planned syntax-level effect.</param>
/// <param name="Path">The source or target path.</param>
/// <param name="DestinationPath">The move destination, when applicable.</param>
/// <param name="Lines">The exact add-file lines.</param>
/// <param name="Hunks">The update hunks.</param>
internal sealed record ParsedPatchEntry(
    ParsedPatchEntryKind Kind,
    FileSystemPath Path,
    FileSystemPath? DestinationPath,
    ImmutableArray<ParsedPatchLine> Lines,
    ImmutableArray<ParsedPatchHunk> Hunks);
