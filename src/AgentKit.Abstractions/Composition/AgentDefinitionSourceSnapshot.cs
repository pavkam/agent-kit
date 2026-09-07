// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// One definition source's complete, immutable contribution to the agent
/// catalog at a point in time.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object. It carries no mutable state and is
/// safe to share across threads without synchronization.
/// </para>
/// <para>
/// A source contributes its whole set rather than incremental edits, so a
/// removed agent actually disappears on recomposition instead of persisting
/// because nobody announced its deletion.
/// </para>
/// </remarks>
public sealed record AgentDefinitionSourceSnapshot
{
    private readonly ImmutableArray<AgentDefinition> _definitions;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AgentDefinitionSourceSnapshot"/> record.
    /// </summary>
    /// <param name="sourceId">The contributing source's identity.</param>
    /// <param name="version">The revision of this contribution.</param>
    /// <param name="precedence">
    /// The source's precedence when two sources publish the same
    /// <see cref="AgentId"/>. Higher wins; equal precedence is a conflict
    /// rather than an arbitrary choice.
    /// </param>
    /// <param name="definitions">
    /// The definitions this source publishes. An empty set is valid.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="definitions"/> is uninitialized, contains
    /// <see langword="null"/>, or contains two definitions with the same
    /// <see cref="AgentDefinition.Id"/>.
    /// </exception>
    public AgentDefinitionSourceSnapshot(
        AgentDefinitionSourceId sourceId,
        AgentDefinitionSourceVersion version,
        int precedence,
        ImmutableArray<AgentDefinition> definitions)
    {
        ArgumentException.ThrowIfContainsNull(definitions);
        ThrowIfDuplicateAgentId(definitions, nameof(definitions));

        SourceId = sourceId;
        Version = version;
        Precedence = precedence;
        _definitions = definitions;
    }

    /// <summary>Gets the contributing source's identity.</summary>
    public AgentDefinitionSourceId SourceId { get; init; }

    /// <summary>Gets the revision of this contribution.</summary>
    public AgentDefinitionSourceVersion Version { get; init; }

    /// <summary>Gets this source's precedence in conflicts.</summary>
    /// <value>
    /// Higher wins. A negative precedence is meaningful and lets a baseline
    /// source sit deliberately below application overrides.
    /// </value>
    public int Precedence { get; init; }

    /// <summary>Gets the definitions this source publishes.</summary>
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
