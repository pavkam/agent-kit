// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>An instruction source whose messages are fixed at definition or registration time.</summary>
public sealed record LiteralInstructionSource: InstructionSource
{
    /// <summary>Initializes one literal instruction source.</summary>
    /// <param name="source">The exact publication identity for this source.</param>
    /// <param name="trust">The provenance trust retained with the source.</param>
    /// <param name="priority">The relative precedence used when ordering sources.</param>
    /// <param name="scope">The narrowest lifecycle scope at which this source applies.</param>
    /// <param name="frequency">How often the source may be re-evaluated during one run.</param>
    /// <param name="messages">The complete system and developer messages contributed by this source.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum field is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="messages"/> is a default array or contains null.</exception>
    public LiteralInstructionSource(
        ContextSourceReference source,
        ContextTrust trust,
        int priority,
        ContextScope scope,
        ContextEvaluationFrequency frequency,
        ImmutableArray<AgentMessage> messages)
        : base(source, trust, priority, scope, frequency)
    {
        ArgumentException.ThrowIfContainsNull(messages);
        Messages = messages;
    }

    /// <summary>Gets the complete system and developer messages contributed by this source.</summary>
    public ImmutableArray<AgentMessage> Messages { get; }
}
