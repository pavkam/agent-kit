// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Globalization;

/// <summary>Projects between legacy instruction messages and typed instruction sources.</summary>
public static class InstructionSourceProjection
{
    /// <summary>The namespace used for definition-authored literal instruction sources.</summary>
    public static readonly ContextSourceNamespace DefinitionNamespace = new("agentkit.agent-definition");

    /// <summary>The source key used when legacy definitions supply only flat instruction messages.</summary>
    public static readonly ContextSourceKey LegacyInstructionsKey = new("instructions");

    /// <summary>Flattens resolved instruction sources into the ordered message list used by the reduced loop.</summary>
    /// <param name="sources">The declared instruction sources.</param>
    /// <returns>Messages in deterministic source order.</returns>
    /// <exception cref="ArgumentException"><paramref name="sources"/> is a default array or contains null.</exception>
    public static ImmutableArray<AgentMessage> ToMessages(ImmutableArray<InstructionSource> sources)
    {
        ArgumentException.ThrowIfContainsNull(sources);
        if (sources.IsEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<AgentMessage>();
        foreach (var source in sources)
        {
            if (source is LiteralInstructionSource literal)
            {
                builder.AddRange(literal.Messages);
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Wraps legacy flat instruction messages as one literal definition source.</summary>
    /// <param name="instructions">The legacy instruction messages.</param>
    /// <param name="revision">The definition revision that owns the messages.</param>
    /// <returns>One literal source, or an empty array when <paramref name="instructions"/> is empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="instructions"/> contains null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revision"/> is default.</exception>
    public static ImmutableArray<InstructionSource> FromLegacyMessages(
        ImmutableArray<AgentMessage> instructions,
        AgentDefinitionRevision revision)
    {
        ArgumentException.ThrowIfContainsNull(instructions);
        ArgumentOutOfRangeException.ThrowIfEqual(revision, default);
        return instructions.IsEmpty
            ? []
            :
        [
            new LiteralInstructionSource(
                new ContextSourceReference(
                    DefinitionNamespace,
                    LegacyInstructionsKey,
                    new ContextSourceVersion(revision.Value.ToString(CultureInfo.InvariantCulture))),
                ContextTrust.AgentDefinition,
                priority: 0,
                ContextScope.Agent,
                ContextEvaluationFrequency.OncePerModelRequest,
                instructions),
        ];
    }
}

