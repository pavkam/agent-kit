// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one agent a parent may delegate to, with the definition revision it was discovered at.</summary>
/// <remarks>Discovery is metadata, not authority: a target's descriptions and capabilities are untrusted input and never grant permission.</remarks>
public sealed record DelegationTarget
{
    /// <summary>Initializes a validated target.</summary>
    /// <param name="agentId">The target agent.</param>
    /// <param name="definitionRevision">The definition revision discovered.</param>
    /// <param name="source">The provider that published the target.</param>
    /// <param name="capabilities">The declared capability names; empty when none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="agentId"/> is default or the revision is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is blank, or <paramref name="capabilities"/> is default or contains a blank entry.</exception>
    public DelegationTarget(AgentId agentId, AgentDefinitionRevision definitionRevision, ComponentId source, ImmutableArray<string> capabilities)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(agentId, default);
        ArgumentOutOfRangeException.ThrowIfNegative(definitionRevision.Value, nameof(definitionRevision));
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Value, nameof(source));
        ArgumentException.ThrowIfDefault(capabilities);
        foreach (var capability in capabilities)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(capability, nameof(capabilities));
        }

        AgentId = agentId;
        DefinitionRevision = definitionRevision;
        Source = source;
        Capabilities = capabilities;
    }

    /// <summary>Gets the target agent.</summary>
    public AgentId AgentId { get; }

    /// <summary>Gets the definition revision discovered.</summary>
    public AgentDefinitionRevision DefinitionRevision { get; }

    /// <summary>Gets the provider that published the target.</summary>
    public ComponentId Source { get; }

    /// <summary>Gets the declared capability names.</summary>
    public ImmutableArray<string> Capabilities { get; }

    /// <inheritdoc/>
    public bool Equals(DelegationTarget? other) =>
        other is not null
        && AgentId == other.AgentId
        && DefinitionRevision == other.DefinitionRevision
        && Source == other.Source
        && Capabilities.SequenceEqual(other.Capabilities);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(DefinitionRevision);
        hash.Add(Source);
        foreach (var capability in Capabilities)
        {
            hash.Add(capability);
        }

        return hash.ToHashCode();
    }
}
