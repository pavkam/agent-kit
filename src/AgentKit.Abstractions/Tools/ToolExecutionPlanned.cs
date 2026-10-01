// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Transfers one prepared call per input call, in input order.</summary>
public sealed record ToolExecutionPlanned: ToolExecutionPlanResult
{
    /// <summary>Wraps the prepared calls.</summary>
    /// <param name="calls">The initialized prepared calls with no null element.</param>
    /// <exception cref="ArgumentException"><paramref name="calls"/> is uninitialized or contains null.</exception>
    public ToolExecutionPlanned(ImmutableArray<PreparedToolCall> calls)
    {
        ArgumentException.ThrowIfDefault(calls);
        ArgumentException.ThrowIfContainsNull(calls);
        Calls = calls;
    }

    /// <summary>Gets the prepared calls.</summary>
    /// <value>One entry per validated call the policy was asked to plan, in the same order; the executor verifies the correspondence.</value>
    public ImmutableArray<PreparedToolCall> Calls { get; }

    /// <summary>Determines structural equality including element order.</summary>
    /// <param name="other">The result to compare, or null.</param>
    /// <returns><see langword="true"/> when both carry equal prepared calls in the same order.</returns>
    public bool Equals(ToolExecutionPlanned? other) => other is not null && Calls.SequenceEqual(other.Calls);

    /// <summary>Returns a hash compatible with <see cref="Equals(ToolExecutionPlanned?)"/>.</summary>
    /// <returns>A hash over the ordered prepared calls.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var call in Calls)
        {
            hash.Add(call);
        }

        return hash.ToHashCode();
    }
}
