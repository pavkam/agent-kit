// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The agent exists and its definition is valid.
/// </summary>
/// <remarks>
/// The catalog version is carried alongside the definition so a caller can
/// record which published revision of the catalog a run was started from,
/// which is what makes a run reproducible after later reconfiguration.
/// </remarks>
public sealed record ResolvedAgentDefinition: AgentDefinitionResolution
{
    private readonly AgentDefinition _definition;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ResolvedAgentDefinition"/> record.
    /// </summary>
    /// <param name="definition">The resolved definition.</param>
    /// <param name="catalogVersion">
    /// The catalog revision the definition was resolved from.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="definition"/> is <see langword="null"/>.
    /// </exception>
    public ResolvedAgentDefinition(
        AgentDefinition definition,
        AgentCatalogVersion catalogVersion)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition = definition;
        CatalogVersion = catalogVersion;
    }

    /// <summary>Gets the resolved definition.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public AgentDefinition Definition
    {
        get => _definition;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Definition));
            _definition = value;
        }
    }

    /// <summary>Gets the catalog revision the definition came from.</summary>
    public AgentCatalogVersion CatalogVersion { get; init; }
}
