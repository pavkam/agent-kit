// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the model surface that may receive retrieved content after exposure authorization.</summary>
public sealed record ModelDestination
{
    /// <summary>Initializes a destination bound to one conversational model alias.</summary>
    /// <param name="modelAlias">The alias of the model that may receive exposed content.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modelAlias"/> is default.</exception>
    public ModelDestination(ModelAlias modelAlias)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(modelAlias, default, nameof(modelAlias));
        ModelAlias = modelAlias;
    }

    /// <summary>Gets the alias of the model that may receive exposed content.</summary>
    public ModelAlias ModelAlias { get; init; }
}
