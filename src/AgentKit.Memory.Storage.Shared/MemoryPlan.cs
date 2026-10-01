// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the outcome of planning one memory mutation: the typed result and the entries an adapter must persist for it.</summary>
/// <remarks>Adapters that persist must write <see cref="Upserts"/> durably and only then commit them to memory, so memory never gets ahead of disk. An empty list means the result is a replay or refusal and nothing changes.</remarks>
/// <typeparam name="TResult">The typed operation result.</typeparam>
/// <param name="Result">The result to return to the caller.</param>
/// <param name="Upserts">The entries to persist atomically, possibly empty.</param>
internal readonly record struct MemoryPlan<TResult>(TResult Result, ImmutableArray<MemoryEntry> Upserts);
