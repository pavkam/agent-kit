// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains the selected continuation cause and every other pending cause for the next request.</summary>
public sealed record ContinuationReason
{
    /// <summary>Initializes a policy-selected continuation reason.</summary>
    /// <param name="selectedCause">The highest-precedence cause selected by policy.</param>
    /// <param name="otherPendingCauses">Every unselected pending cause in original snapshot order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="selectedCause"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="otherPendingCauses"/> is default, contains null, or contains <paramref name="selectedCause"/>.</exception>
    public ContinuationReason(
        RunContinuationCause selectedCause,
        ImmutableArray<RunContinuationCause> otherPendingCauses)
    {
        ArgumentNullException.ThrowIfNull(selectedCause);
        ArgumentException.ThrowIfContainsNull(otherPendingCauses);
        ArgumentException.ThrowIfContainsSelectedCause(selectedCause, otherPendingCauses);
        SelectedCause = selectedCause;
        OtherPendingCauses = otherPendingCauses;
    }

    /// <summary>Gets the highest-precedence selected cause.</summary>
    public RunContinuationCause SelectedCause { get; }
    /// <summary>Gets all remaining causes in captured order.</summary>
    public ImmutableArray<RunContinuationCause> OtherPendingCauses { get; }
}
