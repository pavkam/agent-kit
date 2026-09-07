// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// The first-party embedding model resolver. It maps a selected descriptor
/// to the additively registered <see cref="IEmbeddingModel"/> serving that
/// alias.
/// </summary>
/// <remarks>
/// <para>
/// This registry lives here rather than in any calling component, so no
/// caller ever holds the set of embedding provider adapters directly. It is
/// built once at construction and never mutated, so it is thread-safe and
/// registered as a singleton.
/// </para>
/// <para>
/// Two adapters registered for the same alias is a composition error rather
/// than a last-one-wins merge: silently preferring one would change which
/// provider a call reaches without anyone asking for it.
/// </para>
/// </remarks>
internal sealed class DefaultEmbeddingModelResolver: IEmbeddingModelResolver
{
    private readonly Dictionary<EmbeddingModelAlias, IEmbeddingModel> _models;

    /// <summary>
    /// Initializes the resolver over its additively registered adapters.
    /// </summary>
    /// <param name="models">The registered embedding model adapters.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="models"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="models"/> contains a <see langword="null"/> element
    /// or more than one adapter for the same
    /// <see cref="IEmbeddingModel.Alias"/>.
    /// </exception>
    public DefaultEmbeddingModelResolver(IEnumerable<IEmbeddingModel> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        _models = [];
        foreach (var model in models)
        {
            if (model is null)
            {
                throw new ArgumentException("Value must not contain null elements.", nameof(models));
            }

            if (!_models.TryAdd(model.Alias, model))
            {
                throw new ArgumentException(
                    $"More than one {nameof(IEmbeddingModel)} is registered for alias '{model.Alias}'.",
                    nameof(models));
            }
        }
    }

    /// <inheritdoc/>
    public IEmbeddingModel? Resolve(EmbeddingModelDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return _models.GetValueOrDefault(model.Alias);
    }
}
