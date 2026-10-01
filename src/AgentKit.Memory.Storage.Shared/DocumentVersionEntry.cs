// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is one stored version of a document: its record, complete chunk set, and publication state.</summary>
/// <param name="Record">The version's record.</param>
/// <param name="Chunks">The complete chunk set in ordinal order; empty after a physical purge.</param>
/// <param name="State">The version's publication state.</param>
internal sealed record DocumentVersionEntry(DocumentRecord Record, ImmutableArray<DocumentChunk> Chunks, DocumentVersionState State);
