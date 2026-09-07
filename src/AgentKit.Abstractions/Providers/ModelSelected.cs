// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A compatible model was chosen.
/// </summary>
/// <remarks>
/// Selecting a model grants no authority to reach it. The chosen descriptor
/// still passes capability validation and, at execution time, the normal
/// credential and egress authorization path.
/// </remarks>
public sealed record ModelSelected: ModelSelectionResult
{
    private readonly ModelSelectionDecision _decision;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelSelected"/> record.
    /// </summary>
    /// <param name="decision">The chosen model and its supporting evidence.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="decision"/> is <see langword="null"/>.
    /// </exception>
    public ModelSelected(ModelSelectionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        _decision = decision;
    }

    /// <summary>Gets the chosen model and its supporting evidence.</summary>
    /// <exception cref="ArgumentNullException">
    /// An initializer attempts to set <see langword="null"/>.
    /// </exception>
    public ModelSelectionDecision Decision
    {
        get => _decision;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Decision));
            _decision = value;
        }
    }
}
