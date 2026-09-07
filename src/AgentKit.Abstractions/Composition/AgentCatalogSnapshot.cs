// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable, versioned set of agent definitions one engine currently
/// hosts.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// A snapshot is a stable read. A run that resolves its definition from one
/// version keeps using that definition for its whole lifetime, so a concurrent
/// catalog reload cannot change an agent's behavior midway through a run.
/// </para>
/// </remarks>
public sealed record AgentCatalogSnapshot
{
    private readonly ImmutableArray<AgentDefinition> _definitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCatalogSnapshot"/>
    /// record.
    /// </summary>
    /// <param name="version">This snapshot's catalog revision.</param>
    /// <param name="definitions">
    /// The composed definitions. An empty catalog is structurally valid;
    /// rejecting it is a composition-validation concern rather than a
    /// snapshot invariant.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="definitions"/> is uninitialized, contains
    /// <see langword="null"/>, or contains two definitions with the same
    /// <see cref="AgentDefinition.Id"/>. An ambiguous agent identity would
    /// make resolution non-deterministic.
    /// </exception>
    public AgentCatalogSnapshot(
        AgentCatalogVersion version,
        ImmutableArray<AgentDefinition> definitions)
    {
        ArgumentException.ThrowIfContainsNull(definitions);
        ThrowIfDuplicateAgentId(definitions, nameof(definitions));

        Version = version;
        _definitions = definitions;
    }

    /// <summary>Gets this snapshot's catalog revision.</summary>
    public AgentCatalogVersion Version { get; init; }

    /// <summary>Gets the composed definitions.</summary>
    /// <value>
    /// Agent identities are unique within this collection, so a lookup always
    /// resolves to at most one definition.
    /// </value>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set an uninitialized array, one containing
    /// <see langword="null"/>, or one with a duplicate agent identity.
    /// </exception>
    public ImmutableArray<AgentDefinition> Definitions
    {
        get => _definitions;
        init
        {
            ArgumentException.ThrowIfContainsNull(value, nameof(Definitions));
            ThrowIfDuplicateAgentId(value, nameof(Definitions));
            _definitions = value;
        }
    }

    /// <summary>
    /// Finds the definition published under <paramref name="agentId"/>.
    /// </summary>
    /// <param name="agentId">The agent identity to resolve.</param>
    /// <returns>
    /// The matching definition, or <see langword="null"/> when this snapshot
    /// hosts no agent with that identity.
    /// </returns>
    /// <remarks>
    /// Returning <see langword="null"/> rather than throwing keeps an unknown
    /// agent a typed resolution outcome the caller reports, not an exception
    /// thrown from the middle of a request.
    /// </remarks>
    public AgentDefinition? FindDefinition(AgentId agentId)
    {
        foreach (var definition in _definitions)
        {
            if (definition.Id == agentId)
            {
                return definition;
            }
        }

        return null;
    }

    private static void ThrowIfDuplicateAgentId(
        ImmutableArray<AgentDefinition> definitions,
        string paramName)
    {
        if (definitions.IsDefaultOrEmpty)
        {
            return;
        }

        var seen = new HashSet<AgentId>();
        foreach (var definition in definitions)
        {
            if (!seen.Add(definition.Id))
            {
                throw new ArgumentException(
                    $"Value must not contain duplicate agent id '{definition.Id}'.",
                    paramName);
            }
        }
    }
}
