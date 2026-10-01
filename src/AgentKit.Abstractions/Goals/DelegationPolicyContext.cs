// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Carries the immutable facts a delegation policy evaluates.</summary>
/// <remarks>The context is assembled by the coordinator from durable state and the selected target; a policy cannot mutate it, and it never contains a hook tracker or other live state.</remarks>
public sealed record DelegationPolicyContext
{
    /// <summary>Initializes a validated context.</summary>
    /// <param name="profile">The captured goal profile the parent runs under.</param>
    /// <param name="parent">The parent goal's durable state.</param>
    /// <param name="parentDepth">The non-negative delegation depth of the parent; a root goal has depth zero.</param>
    /// <param name="existingChildren">The non-negative number of children the parent already has.</param>
    /// <param name="target">The selected target.</param>
    /// <param name="now">The evaluation instant from the injected clock.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parentDepth"/> or <paramref name="existingChildren"/> is negative.</exception>
    public DelegationPolicyContext(
        GoalProfileSnapshot profile,
        GoalRecord parent,
        int parentDepth,
        int existingChildren,
        DelegationTarget target,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentOutOfRangeException.ThrowIfNegative(parentDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(existingChildren);
        ArgumentNullException.ThrowIfNull(target);
        Profile = profile;
        Parent = parent;
        ParentDepth = parentDepth;
        ExistingChildren = existingChildren;
        Target = target;
        Now = now;
    }

    /// <summary>Gets the captured goal profile.</summary>
    public GoalProfileSnapshot Profile { get; }

    /// <summary>Gets the parent goal's durable state.</summary>
    public GoalRecord Parent { get; }

    /// <summary>Gets the parent's delegation depth.</summary>
    public int ParentDepth { get; }

    /// <summary>Gets the number of children the parent already has.</summary>
    public int ExistingChildren { get; }

    /// <summary>Gets the selected target.</summary>
    public DelegationTarget Target { get; }

    /// <summary>Gets the evaluation instant.</summary>
    public DateTimeOffset Now { get; }
}
