// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one catalog ambiguity or missing alias target before model exposure.</summary>
/// <remarks>The closed cases preserve exact candidate evidence. A collision is not resolved by its position, a display name, or registration order.</remarks>
public abstract record ToolCatalogCollision
{
    /// <summary>Restricts collision cases to the contract assembly.</summary>
    /// <remarks>Third-party policies consume the closed cases and return bounded selections.</remarks>
    private protected ToolCatalogCollision() { }
}
