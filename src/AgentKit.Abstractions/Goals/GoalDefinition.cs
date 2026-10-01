// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the bounded objective of one goal and the references that supply its context.</summary>
/// <remarks>Context is carried as opaque provenance references rather than an uncontrolled history dump; resolving a reference is a separately authorized operation. The value is immutable and content-bearing, so it is never logged or used as a metric dimension.</remarks>
public sealed record GoalDefinition
{
    /// <summary>Initializes a validated goal definition.</summary>
    /// <param name="objective">The non-blank bounded objective.</param>
    /// <param name="contextReferences">The provenance references that supply context; empty when none.</param>
    /// <param name="extensions">The forward-compatible extension data.</param>
    /// <exception cref="ArgumentException"><paramref name="objective"/> is blank, <paramref name="contextReferences"/> is default, or an entry is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="objective"/>, an entry, or <paramref name="extensions"/> is null.</exception>
    public GoalDefinition(string objective, ImmutableArray<string> contextReferences, ExtensionData extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objective);
        ArgumentException.ThrowIfDefault(contextReferences);
        foreach (var reference in contextReferences)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reference, nameof(contextReferences));
        }

        ArgumentNullException.ThrowIfNull(extensions);
        Objective = objective;
        ContextReferences = contextReferences;
        Extensions = extensions;
    }

    /// <summary>Gets the bounded objective.</summary>
    public string Objective { get; }

    /// <summary>Gets the ordered provenance references that supply context.</summary>
    public ImmutableArray<string> ContextReferences { get; }

    /// <summary>Gets the forward-compatible extension data.</summary>
    public ExtensionData Extensions { get; }

    /// <inheritdoc/>
    public bool Equals(GoalDefinition? other) =>
        other is not null
        && Objective == other.Objective
        && ContextReferences.SequenceEqual(other.ContextReferences)
        && Extensions == other.Extensions;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Objective);
        foreach (var reference in ContextReferences)
        {
            hash.Add(reference);
        }

        hash.Add(Extensions);
        return hash.ToHashCode();
    }
}
