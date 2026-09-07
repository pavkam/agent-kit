// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch;

/// <summary>Contains one host-state-resolved entry before mutation authorization.</summary>
/// <param name="Parsed">The syntax-level source entry.</param>
/// <param name="Id">The fresh mutation identity.</param>
/// <param name="ExpectedContentFingerprint">The required source version when applicable.</param>
/// <param name="FinalContent">The exact final bytes for create or replace.</param>
internal sealed record PlannedPatchEntry(
    ParsedPatchEntry Parsed,
    WorkspaceMutationId Id,
    ContentHash? ExpectedContentFingerprint,
    ImmutableArray<byte> FinalContent);
