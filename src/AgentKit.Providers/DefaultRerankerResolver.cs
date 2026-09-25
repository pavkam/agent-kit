// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>The first-party reranker resolver.</summary>
internal sealed class DefaultRerankerResolver: IRerankerResolver
{
    private readonly Dictionary<RerankerAlias, IReranker> _rerankers;

    /// <summary>Initializes the resolver over registered rerankers.</summary>
    /// <param name="rerankers">The registered reranker adapters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rerankers"/> is null.</exception>
    /// <exception cref="ArgumentException">Duplicate aliases are present.</exception>
    public DefaultRerankerResolver(IEnumerable<IReranker> rerankers)
    {
        ArgumentNullException.ThrowIfNull(rerankers);
        _rerankers = [];
        foreach (var reranker in rerankers)
        {
            if (reranker is null)
            {
                throw new ArgumentException("Value must not contain null elements.", nameof(rerankers));
            }

            if (!_rerankers.TryAdd(reranker.Alias, reranker))
            {
                throw new ArgumentException(
                    $"More than one {nameof(IReranker)} is registered for alias '{reranker.Alias}'.",
                    nameof(rerankers));
            }
        }
    }

    /// <inheritdoc/>
    public IReranker? Resolve(RerankerDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return _rerankers.GetValueOrDefault(model.Alias);
    }
}
