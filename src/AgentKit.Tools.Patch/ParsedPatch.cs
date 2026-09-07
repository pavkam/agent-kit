// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Contains the complete immutable source-ordered result of syntax parsing.</summary>
/// <param name="Entries">The non-empty parsed entries.</param>
internal sealed record ParsedPatch(ImmutableArray<ParsedPatchEntry> Entries);
