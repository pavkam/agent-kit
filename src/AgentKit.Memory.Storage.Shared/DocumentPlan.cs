// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the outcome of planning one document mutation: the typed result and the entry an adapter must persist for it.</summary>
/// <remarks>Adapters that persist must write <see cref="Upsert"/> durably and only then commit it to memory. A <see langword="null"/> entry means the result is a replay or refusal and nothing changes.</remarks>
/// <typeparam name="TResult">The typed operation result.</typeparam>
/// <param name="Result">The result to return to the caller.</param>
/// <param name="Upsert">The entry to persist, or <see langword="null"/>.</param>
internal readonly record struct DocumentPlan<TResult>(TResult Result, DocumentEntry? Upsert);
