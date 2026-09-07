// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Contains one source-ordered exact context/removal/addition hunk.</summary>
/// <param name="Lines">The non-empty prefixed logical lines.</param>
internal sealed record ParsedPatchHunk(ImmutableArray<ParsedPatchLine> Lines);
