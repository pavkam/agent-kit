// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is one persisted log record of a durable memory store: the entries one mutation changed, written atomically as a single line.</summary>
/// <param name="Entries">The changed entries; a correction changes two.</param>
internal sealed record MemoryLogDocument(ImmutableArray<MemoryEntryDocument> Entries);
