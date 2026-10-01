// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Is one atomically persisted group of entries, appended as a single record so a plan commits or is absent as a whole.</summary>
/// <param name="Entries">The entries to insert or replace, in commit order.</param>
internal sealed record StoredArtifactLogRecord(IReadOnlyList<StoredArtifactEntry> Entries);
