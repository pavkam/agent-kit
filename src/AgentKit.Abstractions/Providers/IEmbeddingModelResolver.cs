// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Resolves a selected embedding model descriptor to the concrete adapter
/// that can execute one attempt against it.
/// </summary>
/// <remarks>
/// <para>
/// This is the embedding-generation analog of <see cref="ILlmModelResolver"/>,
/// not a reuse of it: embedding generation is a separate provider contract
/// with its own alias space and registered adapter type.
/// </para>
/// <para>
/// Implementations must be thread-safe and are normally registered as
/// singletons, because one engine resolves adapters concurrently for many
/// callers.
/// </para>
/// <para>
/// Resolution is a lookup, not a factory. It performs no provider I/O,
/// resolves no credentials, and creates no connection; the returned adapter
/// owns all of that when it is actually invoked.
/// </para>
/// </remarks>
public interface IEmbeddingModelResolver
{
    /// <summary>
    /// Finds the adapter registered to serve <paramref name="model"/>.
    /// </summary>
    /// <param name="model">The descriptor chosen by selection.</param>
    /// <returns>
    /// The adapter serving the descriptor's
    /// <see cref="EmbeddingModelDescriptor.Alias"/>, or
    /// <see langword="null"/> when no adapter is registered for it.
    /// </returns>
    /// <remarks>
    /// A <see langword="null"/> result means the catalog and the registered
    /// adapters disagree: a model was configured as available but nothing
    /// can execute it. Callers surface that as a typed outcome rather than
    /// an exception, because it is a composition mistake that should be
    /// reportable rather than fatal mid-call.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/> is <see langword="null"/>.
    /// </exception>
    public IEmbeddingModel? Resolve(EmbeddingModelDescriptor model);
}
