// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One declared instruction contribution with provenance and precedence evidence.</summary>
/// <remarks>
/// Instruction sources retain identity until provider translation. This hierarchy is closed in the
/// abstractions assembly; only <see cref="LiteralInstructionSource"/> ships in the first wave.
/// Additional source kinds arrive through later context workstreams.
/// </remarks>
public abstract record InstructionSource
{
    /// <summary>Initializes shared instruction-source metadata.</summary>
    /// <param name="source">The exact publication identity for this source.</param>
    /// <param name="trust">The provenance trust retained with the source.</param>
    /// <param name="priority">The relative precedence used when ordering sources.</param>
    /// <param name="scope">The narrowest lifecycle scope at which this source applies.</param>
    /// <param name="frequency">How often the source may be re-evaluated during one run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum field is undefined.</exception>
    private protected InstructionSource(
        ContextSourceReference source,
        ContextTrust trust,
        int priority,
        ContextScope scope,
        ContextEvaluationFrequency frequency)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfUndefined(trust);
        ArgumentOutOfRangeException.ThrowIfUndefined(scope);
        ArgumentOutOfRangeException.ThrowIfUndefined(frequency);
        Source = source;
        Trust = trust;
        Priority = priority;
        Scope = scope;
        Frequency = frequency;
    }

    /// <summary>Gets the exact publication identity for this source.</summary>
    public ContextSourceReference Source { get; }

    /// <summary>Gets the provenance trust retained with the source.</summary>
    public ContextTrust Trust { get; }

    /// <summary>Gets the relative precedence used when ordering sources.</summary>
    /// <value>Higher values are resolved before lower values; ties preserve declaration order.</value>
    public int Priority { get; }

    /// <summary>Gets the narrowest lifecycle scope at which this source applies.</summary>
    public ContextScope Scope { get; }

    /// <summary>Gets how often the source may be re-evaluated during one run.</summary>
    public ContextEvaluationFrequency Frequency { get; }
}
