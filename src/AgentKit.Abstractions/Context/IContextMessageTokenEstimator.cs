// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Estimates advisory token counts for context budgeting and pressure checks.</summary>
public interface IContextMessageTokenEstimator
{
    /// <summary>Estimates tokens from message content parts.</summary>
    /// <param name="messages">The messages to measure.</param>
    /// <returns>An advisory estimate.</returns>
    /// <exception cref="ArgumentException"><paramref name="messages"/> is a default, uninitialized array.</exception>
    public long EstimateTokens(ImmutableArray<AgentMessage> messages);

    /// <summary>Estimates tokens from standalone content parts.</summary>
    /// <param name="parts">The parts to measure.</param>
    /// <returns>An advisory estimate.</returns>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is a default, uninitialized array.</exception>
    public long EstimateTokens(ImmutableArray<ContentPart> parts);
}
