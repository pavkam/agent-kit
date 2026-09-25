// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves a selected reranker descriptor to a concrete adapter.</summary>
public interface IRerankerResolver
{
    /// <summary>Resolves <paramref name="model"/> to an adapter instance.</summary>
    /// <param name="model">The selected descriptor.</param>
    /// <returns>The adapter, or <see langword="null"/> when none is registered.</returns>
    public IReranker? Resolve(RerankerDescriptor model);
}
