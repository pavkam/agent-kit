// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for the outcome of resolving one agent identity to a runnable handle.</summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are <see cref="ResolvedAgent"/>,
/// <see cref="AgentNotFound"/>, and <see cref="InvalidAgent"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside AgentKit can add a fourth kind and defeat
/// exhaustive handling. This is distinct from <see cref="AgentDefinitionResolution"/>: that family resolves a
/// catalog identity to a definition, before any facade handle exists; this family resolves an identity to a live,
/// engine-bound <see cref="Agent"/> handle.
/// </remarks>
public abstract record AgentResolution
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentResolution"/> record. This constructor is
    /// <see langword="private protected"/> so only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected AgentResolution()
    {
    }
}
