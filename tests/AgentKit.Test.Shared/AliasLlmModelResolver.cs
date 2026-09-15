// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// An <see cref="ILlmModelResolver"/> test double that maps a descriptor to the supplied adapter whose
/// <see cref="ILlmModel.Alias"/> matches, and to null otherwise.
/// </summary>
public sealed class AliasLlmModelResolver: ILlmModelResolver
{
    private readonly ILlmModel[] _adapters;

    /// <summary>Initializes a new instance of the <see cref="AliasLlmModelResolver"/> class.</summary>
    /// <param name="adapters">The adapters to resolve by alias; may be empty to resolve nothing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="adapters"/> is null or contains a null element.</exception>
    public AliasLlmModelResolver(params ILlmModel[] adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        foreach (var adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter, nameof(adapters));
        }

        _adapters = adapters;
    }

    /// <inheritdoc/>
    public ILlmModel? Resolve(ModelDescriptor model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return Array.Find(_adapters, adapter => adapter.Alias == model.Alias);
    }
}
