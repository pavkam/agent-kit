// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the authoritative, content-free evidence that a document was logically deleted.</summary>
/// <param name="DeletedAt">The instant of logical deletion.</param>
/// <param name="Generation">The store-wide deletion generation assigned at logical deletion.</param>
/// <param name="Purged">Whether every version's chunks were physically removed.</param>
internal sealed record DocumentDeletion(DateTimeOffset DeletedAt, long Generation, bool Purged);
