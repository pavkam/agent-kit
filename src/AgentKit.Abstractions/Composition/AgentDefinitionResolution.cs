// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the outcome of resolving one agent identity against
/// the catalog.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="ResolvedAgentDefinition"/>,
/// <see cref="AgentDefinitionNotFound"/>, and
/// <see cref="InvalidAgentDefinition"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a fourth kind and defeat exhaustive
/// handling.
/// </remarks>
public abstract record AgentDefinitionResolution
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AgentDefinitionResolution"/> record. This constructor is
    /// <see langword="private protected"/> so only the closed set of kinds
    /// declared in this assembly can extend the hierarchy.
    /// </summary>
    private protected AgentDefinitionResolution()
    {
    }
}
