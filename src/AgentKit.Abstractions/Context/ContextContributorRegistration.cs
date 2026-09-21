// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares one <see cref="IContextContributor"/>'s stable identity, evaluation cadence, and failure posture.</summary>
/// <remarks>
/// Registrations are additive under one assembler key and ordered by <see cref="Order"/>; duplicate
/// <see cref="ContributorId"/> values with different implementation types fail composition rather than silently
/// replacing an earlier registration.
/// </remarks>
public sealed record ContextContributorRegistration
{
    /// <summary>Initializes one contributor registration.</summary>
    /// <param name="contributorId">The non-default source key that uniquely identifies this contributor within the assembler profile.</param>
    /// <param name="order">The contributor's position in the deterministic evaluation order; lower values run first.</param>
    /// <param name="frequency">How often the contributor is evaluated during one run.</param>
    /// <param name="required">When <see langword="true"/>, contributor failure fails assembly; optional contributors may be omitted on failure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="contributorId"/> is default or <paramref name="frequency"/> is undefined.</exception>
    public ContextContributorRegistration(
        ContextSourceKey contributorId,
        int order,
        ContextEvaluationFrequency frequency,
        bool required)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(contributorId, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(frequency);
        ContributorId = contributorId;
        Order = order;
        Frequency = frequency;
        Required = required;
    }

    /// <summary>Gets the source key that uniquely identifies this contributor within the assembler profile.</summary>
    public ContextSourceKey ContributorId { get; }

    /// <summary>Gets this contributor's position in the deterministic evaluation order.</summary>
    /// <value>Lower values are evaluated first; ties are broken by registration order.</value>
    public int Order { get; }

    /// <summary>Gets how often the contributor is evaluated during one run.</summary>
    public ContextEvaluationFrequency Frequency { get; }

    /// <summary>Gets whether contributor failure must fail assembly.</summary>
    public bool Required { get; }
}
