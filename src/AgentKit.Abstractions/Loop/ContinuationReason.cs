// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Retains the policy-selected continuation cause together with pending evidence that remains relevant to the next request.</summary>
/// <remarks>Selection records precedence for one proposal. It does not discard the unselected causes, execute the selected cause, or prevent session-owner revalidation.</remarks>
public sealed record ContinuationReason
{
    /// <summary>Initializes the reason for one proposed continuation.</summary>
    /// <param name="selectedCause">The non-null pending cause selected by policy according to its precedence rules.</param>
    /// <param name="otherPendingCauses">A non-default immutable collection of unselected causes in their original snapshot order, excluding <paramref name="selectedCause"/>.</param>
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

    /// <summary>Gets the pending cause selected to explain this proposed continuation.</summary>
    /// <value>A non-null immutable cause selected by policy; it does not grant authority to perform another request.</value>
    public RunContinuationCause SelectedCause { get; }
    /// <summary>Gets the remaining pending causes in their captured snapshot order.</summary>
    /// <value>A non-default immutable array with no null entries and without <see cref="SelectedCause"/>.</value>
    public ImmutableArray<RunContinuationCause> OtherPendingCauses { get; }
}
